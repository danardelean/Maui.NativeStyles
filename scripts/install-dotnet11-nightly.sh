#!/usr/bin/env bash
# Installs a .NET 11 nightly SDK into ./.dotnet (repository root) and the maui workload into that same folder.
# Nothing is written to /usr/local/share/dotnet. Remove ./.dotnet and ./global.json to undo.
#
#   ./scripts/install-dotnet11-nightly.sh                      # newest daily of the RC2 lane (iOS workload built for Xcode 27)
#   DOTNET_VERSION=11.0.100-rc.2.26504.105 ./scripts/...       # a specific daily (the one dotnet/maui's RC2 branch uses)
#   DOTNET_CHANNEL=11.0.1xx ./scripts/...                      # main (RTM) lane instead of RC2
#   NET10_COMPAT_VERSION=10.0.12 ./scripts/...                 # .NET 10 runtime packs for the net10 compat workloads (default: newest on nuget.org)
#
# How it works: the daily SDK ships stale baseline workload manifests (preview-era iOS/MAUI versions), so the newest
# manifests of the SDK's version band are downloaded from the dotnet11 feed into the SDK's sdk-manifests folder (what
# dotnet/maui's own build does), the net10 compat manifests are pointed at a public .NET 10 runtime, and the packs are
# then installed with --skip-manifest-update so exactly those manifests are used.
set -euo pipefail
cd "$(dirname "$0")/.."

DOTNET_DIR="$PWD/.dotnet"
CHANNEL="${DOTNET_CHANNEL:-11.0.1xx-rc2}"
VERSION="${DOTNET_VERSION:-latest}"
DOTNET11_FEED="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json"
DOTNET11_FLAT="${DOTNET11_FLAT:-https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/flat2}"
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
[ -d "$MANIFESTS_DIR" ] || { echo "No manifests folder $MANIFESTS_DIR" >&2; exit 1; }

# ---- 2. Newest manifests of this band from the dotnet11 feed -----------------------------------------------------------
# NuGet flat container: <flat>/<id>/index.json lists the versions in ascending order, <flat>/<id>/<v>/<id>.<v>.nupkg is the package.
newest_version() {
  curl -sSfL "$DOTNET11_FLAT/$1/index.json" 2>/dev/null | tr -d ' \n' | grep -o '"versions":\[[^]]*\]' | grep -o '"[^"]*"' | tr -d '"' | tail -1
}
for dir in "$MANIFESTS_DIR"/*/; do
  id="$(basename "$dir")"                                   # e.g. microsoft.net.sdk.ios
  [ "$id" = "workloadsets" ] && continue
  pkg="$id.manifest-$BAND"                                  # e.g. microsoft.net.sdk.ios.manifest-11.0.100-rc.2
  ver="$(newest_version "$pkg" || true)"
  if [ -z "$ver" ]; then
    echo "$id: nothing on the dotnet11 feed for $pkg, keeping the SDK's $(ls "$dir" | tail -1)"
    continue
  fi
  if [ -d "$dir/$ver" ]; then
    echo "$id: $ver already present"
    continue
  fi
  echo "$id: $ver"
  curl -sSfL "$DOTNET11_FLAT/$pkg/$ver/$pkg.$ver.nupkg" -o "$work/$pkg.nupkg"
  rm -rf "$work/data" && unzip -q -o "$work/$pkg.nupkg" 'data/*' -d "$work"
  mkdir -p "$dir/$ver" && cp -R "$work/data/." "$dir/$ver/"
  # The resolver takes the highest version folder; drop the older ones so there is no ambiguity
  for old in "$dir"/*/; do [ "$(basename "$old")" = "$ver" ] || rm -rf "$old"; done
done

# ---- 3. net10 compat manifests -----------------------------------------------------------------------------------------
# microsoft-net-runtime-*-net10 (pulled in by the maui/android/ios workloads for net10.0 apps) reference the .NET 10
# runtime the SDK was built with, which can be a servicing release that is not public yet (10.0.13 before Patch Tuesday).
# This repository builds net11.0, so point them at the newest public .NET 10 runtime instead.
if [ -z "${NET10_COMPAT_VERSION:-}" ]; then
  NET10_COMPAT_VERSION="$(curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.mono.android-arm64/index.json \
    | tr -d ' \n' | grep -o '"10\.0\.[0-9]*"' | tr -d '"' | sort -t. -k3,3n | tail -1)"
fi
for manifest in "$MANIFESTS_DIR"/microsoft.net.workload.mono.toolchain.net10/*/WorkloadManifest.json \
                "$MANIFESTS_DIR"/microsoft.net.workload.emscripten.net10/*/WorkloadManifest.json; do
  [ -f "$manifest" ] || continue
  current="$(grep -o '"version" *: *"10\.0\.[0-9]*"' "$manifest" | head -1 | grep -o '10\.0\.[0-9]*' || true)"
  if [ -n "$current" ] && [ "$current" != "$NET10_COMPAT_VERSION" ]; then
    echo "net10 compat manifest $(basename "$(dirname "$(dirname "$manifest")")"): .NET $current -> $NET10_COMPAT_VERSION"
    perl -pi -e "s/\"\Q$current\E\"/\"$NET10_COMPAT_VERSION\"/g" "$manifest"
  fi
done

# ---- 4. Packs ----------------------------------------------------------------------------------------------------------
# Workloads land in ./.dotnet/sdk-manifests, ./.dotnet/packs and ./.dotnet/metadata: fully in-folder.
# --skip-manifest-update uses the manifests placed above; --source replaces the configured sources, so nuget.org is repeated.
"$DOTNET_DIR/dotnet" workload install maui --skip-manifest-update --skip-sign-check \
  --source "$DOTNET11_FEED" --source "$NUGET_ORG"
"$DOTNET_DIR/dotnet" workload list

# ---- 5. global.json ----------------------------------------------------------------------------------------------------
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
echo "iOS workload: $(ls "$MANIFESTS_DIR/microsoft.net.sdk.ios")  (27.0.* = built for Xcode 27)"
