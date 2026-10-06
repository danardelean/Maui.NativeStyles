#!/usr/bin/env bash
# Installs a .NET 11 nightly SDK into ./.dotnet (repository root) and the maui workload into that same folder.
# Nothing is written to /usr/local/share/dotnet. Remove ./.dotnet and ./global.json to undo.
#
#   ./scripts/install-dotnet11-nightly.sh                      # newest daily of the RC2 lane (iOS workload built for Xcode 27)
#   DOTNET_VERSION=11.0.100-rc.2.26504.105 ./scripts/...       # a specific daily (the one dotnet/maui's RC2 branch uses)
#   DOTNET_CHANNEL=11.0.1xx MAUI_BRANCH=net11.0 ./scripts/...  # main (RTM) lane instead of RC2
#   NET10_COMPAT_VERSION=10.0.12 ./scripts/...                 # .NET 10 runtime packs for the net10 compat workloads (default: newest on nuget.org)
#
# How it works. The daily SDK bundles stale baseline workload manifests (preview-era iOS / MAUI, in an older band folder
# that the SDK falls back to), so for every manifest the SDK lists in KnownWorkloadManifests.txt the newest package
# of the SDK's own version band is downloaded into sdk-manifests/<band>/ (what dotnet/maui's own build does). The feeds
# searched are dotnet11 plus the feeds dotnet/maui's RC2 branch builds against (its NuGet.config), because macios / android
# release-branch builds land on isolated darc-pub-* feeds. The net10 compat manifests are then pointed at a public .NET 10
# runtime and the packs are installed with --skip-manifest-update, so exactly those manifests are used.
set -euo pipefail
cd "$(dirname "$0")/.."

DOTNET_DIR="$PWD/.dotnet"
CHANNEL="${DOTNET_CHANNEL:-11.0.1xx-rc2}"
VERSION="${DOTNET_VERSION:-latest}"
MAUI_BRANCH="${MAUI_BRANCH:-release/11.0.1xx-rc2}"
DOTNET11_FEED="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json"
NUGET_ORG="https://api.nuget.org/v3/index.json"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# ---- 1. SDK -----------------------------------------------------------------------------------------------------------
curl -sSL https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh -o "$work/dotnet-install.sh"
if [ "$VERSION" = "latest" ]; then
  # --quality daily resolves https://aka.ms/dotnet/<channel>/daily/dotnet-sdk-osx-<arch>.tar.gz
  bash "$work/dotnet-install.sh" --channel "$CHANNEL" --quality daily --install-dir "$DOTNET_DIR" --no-path
else
  # --version and --quality are mutually exclusive; dailies are served from ci.dot.net/public, one of the script's default feeds
  bash "$work/dotnet-install.sh" --version "$VERSION" --install-dir "$DOTNET_DIR" --no-path
fi

export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
SDK_VERSION="$("$DOTNET_DIR/dotnet" --version)"
# Version band of the SDK: 11.0.100-rc.2.26504.105 -> 11.0.100-rc.2, 11.0.100-rtm.26480.113 -> 11.0.100
BAND="$(echo "$SDK_VERSION" | sed -E 's/^([0-9]+\.[0-9]+\.[0-9])[0-9][0-9](-(preview|rc|alpha)\.[0-9]+)?.*$/\100\2/')"
MANIFESTS_DIR="$DOTNET_DIR/sdk-manifests/$BAND"
echo "SDK $SDK_VERSION (band $BAND) in $DOTNET_DIR"
mkdir -p "$MANIFESTS_DIR"
# The manifest ids the SDK resolves: KnownWorkloadManifests.txt (IncludedWorkloadManifests.txt in older SDKs), else
# whatever manifest folders the SDK ships in any band.
INCLUDED="$work/manifest-ids.txt"
if [ -f "$DOTNET_DIR/sdk/$SDK_VERSION/KnownWorkloadManifests.txt" ]; then
  cp "$DOTNET_DIR/sdk/$SDK_VERSION/KnownWorkloadManifests.txt" "$INCLUDED"
elif [ -f "$DOTNET_DIR/sdk/$SDK_VERSION/IncludedWorkloadManifests.txt" ]; then
  cp "$DOTNET_DIR/sdk/$SDK_VERSION/IncludedWorkloadManifests.txt" "$INCLUDED"
else
  ls -d "$DOTNET_DIR"/sdk-manifests/*/*/ | xargs -n1 basename | grep -v '^workloadsets$' | sort -u > "$INCLUDED"
fi
echo "Manifests: $(tr '\r\n' '  ' < "$INCLUDED")"

# ---- 2. Feeds ----------------------------------------------------------------------------------------------------------
# dotnet11 first, then every dnceng feed of dotnet/maui's branch (dotnet11-transport and the isolated darc-pub-* feeds).
FEEDS=("$DOTNET11_FEED")
maui_feeds="$(curl -sSfL "https://raw.githubusercontent.com/dotnet/maui/$MAUI_BRANCH/NuGet.config" \
  | grep -o 'value="https://pkgs.dev.azure.com/dnceng/public/_packaging/[^"]*"' | cut -d'"' -f2 \
  | grep -E '/(dotnet11[^/]*|darc-pub-[^/]*)/nuget/v3/index.json$' || true)"
for f in $maui_feeds; do
  case " ${FEEDS[*]} " in *" $f "*) ;; *) FEEDS+=("$f") ;; esac
done
echo "Feeds:"; printf '  %s\n' "${FEEDS[@]}"

# NuGet v3 flat container: <feed>/flat2/<id>/index.json lists the versions, <feed>/flat2/<id>/<v>/<id>.<v>.nupkg is the package.
flat() { echo "${1%/index.json}/flat2"; }
versions() { curl -sSfL "$(flat "$1")/$2/index.json" 2>/dev/null | tr -d ' \n' | grep -o '"versions":\[[^]]*\]' | grep -o '"[^"]*"' | tr -d '"' || true; }
# Reads "version<TAB>feed" lines and prints the one with the highest version, by Semantic Versioning precedence:
# numeric core first, then a release above its prereleases, then prerelease fields (numeric < alphanumeric, so
# 11.0.0-rc.2.26504.105 > 11.0.0-preview.7.26471.7 and 27.0.12211-net11-rc.2 > 26.5.12253-net11-rc.2).
pick_highest() {
  perl -e '
    sub cmpv {
      my ($v1, $v2) = @_;
      my ($c1, $p1) = split /-/, $v1, 2; my ($c2, $p2) = split /-/, $v2, 2;
      my @n1 = split /\./, $c1; my @n2 = split /\./, $c2;
      for my $i (0..2) { my $c = ($n1[$i] // 0) <=> ($n2[$i] // 0); return $c if $c; }
      return 0 if !defined $p1 && !defined $p2; return 1 if !defined $p1; return -1 if !defined $p2;
      my @f1 = split /\./, $p1; my @f2 = split /\./, $p2;
      for my $i (0..($#f1 > $#f2 ? $#f1 : $#f2)) {
        my ($x, $y) = ($f1[$i], $f2[$i]);
        return -1 if !defined $x; return 1 if !defined $y;
        my $c = ($x =~ /^\d+$/ && $y =~ /^\d+$/) ? $x <=> $y : ($x =~ /^\d+$/ ? -1 : ($y =~ /^\d+$/ ? 1 : $x cmp $y));
        return $c if $c;
      }
      return 0;
    }
    my @lines = grep { /\S/ } map { chomp; $_ } <STDIN>;
    my ($best) = sort { cmpv((split /\t/, $b)[0], (split /\t/, $a)[0]) } @lines;
    print "$best\n" if defined $best;'
}

# ---- 3. Newest manifests of this band ----------------------------------------------------------------------------------
while IFS= read -r id || [ -n "$id" ]; do
  id="$(echo "$id" | tr -d '\r' | tr '[:upper:]' '[:lower:]')"
  [ -z "$id" ] && continue
  pkg="$id.manifest-$BAND"                                  # e.g. microsoft.net.sdk.ios.manifest-11.0.100-rc.2
  : > "$work/candidates"
  for feed in "${FEEDS[@]}"; do
    for v in $(versions "$feed" "$pkg"); do printf '%s\t%s\n' "$v" "$feed" >> "$work/candidates"; done
  done
  chosen="$(pick_highest < "$work/candidates")"
  best="${chosen%%	*}"; best_feed="${chosen#*	}"
  if [ -z "$best" ]; then
    have="$(ls -d "$DOTNET_DIR"/sdk-manifests/*/"$id"/*/ 2>/dev/null | tail -1 || true)"
    echo "$id: no $pkg on any feed, keeping the SDK's ${have:-<none>}"
    continue
  fi
  echo "$id: $best  ($(echo "$best_feed" | sed -E 's#.*/_packaging/([^/]*)/.*#\1#'))"
  [ -d "$MANIFESTS_DIR/$id/$best" ] && continue
  curl -sSfL "$(flat "$best_feed")/$pkg/$best/$pkg.$best.nupkg" -o "$work/$pkg.nupkg"
  rm -rf "$work/data" && unzip -q -o "$work/$pkg.nupkg" 'data/*' -d "$work"
  rm -rf "$MANIFESTS_DIR/$id" && mkdir -p "$MANIFESTS_DIR/$id/$best" && cp -R "$work/data/." "$MANIFESTS_DIR/$id/$best/"
done < "$INCLUDED"
# Pinned manifest versions from an earlier install would override the folders above
rm -f "$DOTNET_DIR/metadata/workloads/$BAND/InstallState/default.json"

# ---- 4. net10 compat manifests -----------------------------------------------------------------------------------------
# microsoft-net-runtime-*-net10 (pulled in by the maui/android/ios workloads for net10.0 apps) reference the .NET 10
# runtime the SDK was built with, which can be a servicing release that is not public yet (10.0.13 before Patch Tuesday).
# This repository builds net11.0, so point them at the newest public .NET 10 runtime instead.
if [ -z "${NET10_COMPAT_VERSION:-}" ]; then
  NET10_COMPAT_VERSION="$(curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.mono.android-arm64/index.json \
    | tr -d ' \n' | grep -o '"10\.0\.[0-9]*"' | tr -d '"' | sort -t. -k3,3n | tail -1)"
fi
for manifest in "$DOTNET_DIR"/sdk-manifests/*/microsoft.net.workload.mono.toolchain.net10/*/WorkloadManifest.json \
                "$DOTNET_DIR"/sdk-manifests/*/microsoft.net.workload.emscripten.net10/*/WorkloadManifest.json; do
  [ -f "$manifest" ] || continue
  current="$(grep -o '"version" *: *"10\.0\.[0-9]*"' "$manifest" | head -1 | grep -o '10\.0\.[0-9]*' || true)"
  if [ -n "$current" ] && [ "$current" != "$NET10_COMPAT_VERSION" ]; then
    echo "net10 compat manifest $(basename "$(dirname "$(dirname "$manifest")")"): .NET $current -> $NET10_COMPAT_VERSION"
    perl -pi -e "s/\"\Q$current\E\"/\"$NET10_COMPAT_VERSION\"/g" "$manifest"
  fi
done

# ---- 5. Packs ----------------------------------------------------------------------------------------------------------
# Workloads land in ./.dotnet/sdk-manifests, ./.dotnet/packs and ./.dotnet/metadata: fully in-folder.
# --skip-manifest-update uses the manifests placed above; --source replaces the configured sources, so nuget.org is repeated.
sources=()
for feed in "${FEEDS[@]}" "$NUGET_ORG"; do sources+=(--source "$feed"); done
"$DOTNET_DIR/dotnet" workload install maui --skip-manifest-update --skip-sign-check "${sources[@]}"
# Packs of manifest versions replaced above (an earlier run) are no longer referenced: drop them
"$DOTNET_DIR/dotnet" workload clean >/dev/null || true
"$DOTNET_DIR/dotnet" workload list

# ---- 6. global.json ----------------------------------------------------------------------------------------------------
# The system `dotnet` (a .NET 10 SDK host) picks the SDK from ./.dotnet through global.json "paths"; keep this file out of git.
cat > global.json <<JSON
{
  "sdk": {
    "version": "$SDK_VERSION",
    "paths": [ ".dotnet", "\$host\$" ],
    "errorMessage": "The .NET 11 nightly SDK was not found in ./.dotnet: run ./scripts/install-dotnet11-nightly.sh"
  }
}
JSON
echo "Wrote global.json pinned to $SDK_VERSION"
echo "Manifests in $MANIFESTS_DIR (iOS 27.0.* = built for Xcode 27):"
for d in "$MANIFESTS_DIR"/*/; do [ "$(basename "$d")" = "workloadsets" ] || echo "  $(basename "$d"): $(ls "$d" | tr '\n' ' ')"; done
