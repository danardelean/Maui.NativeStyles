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
# that the SDK falls back to), so for every manifest the SDK lists in IncludedWorkloadManifests.txt the newest package
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
INCLUDED="$DOTNET_DIR/sdk/$SDK_VERSION/IncludedWorkloadManifests.txt"
echo "SDK $SDK_VERSION (band $BAND) in $DOTNET_DIR"
[ -f "$INCLUDED" ] || { echo "Missing $INCLUDED" >&2; exit 1; }
mkdir -p "$MANIFESTS_DIR"

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
# Highest of two versions by their numeric fields (27.0.12211-net11-rc.2 > 26.5.12253-net11-rc.2; 11.0.0-rc.2.26504.105 > 11.0.0-rc.2.26480.1)
higher() { printf '%s\n%s\n' "$1" "$2" | sort -t. -k1,1n -k2,2n -k3,3n -k4,4n -k5,5n -k6,6n | tail -1; }

# ---- 3. Newest manifests of this band ----------------------------------------------------------------------------------
while IFS= read -r id || [ -n "$id" ]; do
  id="$(echo "$id" | tr -d '\r' | tr '[:upper:]' '[:lower:]')"
  [ -z "$id" ] && continue
  pkg="$id.manifest-$BAND"                                  # e.g. microsoft.net.sdk.ios.manifest-11.0.100-rc.2
  best="" best_feed=""
  for feed in "${FEEDS[@]}"; do
    for v in $(versions "$feed" "$pkg"); do
      if [ -z "$best" ] || [ "$(higher "$best" "$v")" = "$v" ]; then best="$v"; best_feed="$feed"; fi
    done
  done
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
echo "iOS workload manifest: $(ls "$MANIFESTS_DIR/microsoft.net.sdk.ios" 2>/dev/null || echo '<not in band folder>')  (27.0.* = built for Xcode 27)"
