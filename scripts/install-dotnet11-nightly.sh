#!/usr/bin/env bash
# Installs a .NET 11 nightly SDK into ./.dotnet (repository root) and the maui workload into that same folder.
# Nothing is written to /usr/local/share/dotnet. Remove ./.dotnet and ./global.json to undo.
#
#   ./scripts/install-dotnet11-nightly.sh                      # newest daily of the RC2 lane (iOS workload built for Xcode 27)
#   DOTNET_VERSION=11.0.100-rc.2.26504.105 ./scripts/...       # a specific daily (the one dotnet/maui's RC2 branch uses)
#   DOTNET_CHANNEL=11.0.1xx ./scripts/...                      # main (RTM) lane instead of RC2
#   NET10_COMPAT_VERSION=10.0.12 ./scripts/...                 # .NET 10 runtime packs for the net10 compat workloads (default: newest on nuget.org)
set -euo pipefail
cd "$(dirname "$0")/.."

DOTNET_DIR="$PWD/.dotnet"
CHANNEL="${DOTNET_CHANNEL:-11.0.1xx-rc2}"
VERSION="${DOTNET_VERSION:-latest}"
DOTNET11_FEED="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json"
NUGET_ORG="https://api.nuget.org/v3/index.json"

work="$(mktemp -d)"
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
echo "SDK $SDK_VERSION in $DOTNET_DIR"

# The daily SDK ships its workload manifests (ios, android, maui, ... of this band) in sdk-manifests/. Its net10 compat
# manifests (microsoft-net-runtime-*-net10, pulled in by the maui workload for net10.0 apps) reference the .NET 10 runtime
# packs the SDK was built with, which can be a servicing release that is not on nuget.org yet (10.0.13 before Patch
# Tuesday). Point them at the newest public .NET 10 runtime instead: this repository builds net11.0, so they are not used.
if [ -z "${NET10_COMPAT_VERSION:-}" ]; then
  NET10_COMPAT_VERSION="$(curl -sSL https://api.nuget.org/v3-flatcontainer/microsoft.netcore.app.runtime.mono.android-arm64/index.json \
    | tr -d ' \n' | grep -o '"10\.0\.[0-9]*"' | tr -d '"' | sort -t. -k3,3n | tail -1)"
fi
band_dir="$(ls -d "$DOTNET_DIR"/sdk-manifests/11.0.* | head -1)"
for manifest in "$band_dir"/microsoft.net.workload.mono.toolchain.net10/*/WorkloadManifest.json \
                "$band_dir"/microsoft.net.workload.emscripten.net10/*/WorkloadManifest.json; do
  [ -f "$manifest" ] || continue
  current="$(grep -o '"version": *"10\.0\.[0-9]*"' "$manifest" | head -1 | grep -o '10\.0\.[0-9]*' || true)"
  if [ -n "$current" ] && [ "$current" != "$NET10_COMPAT_VERSION" ]; then
    echo "net10 compat manifest $(basename "$(dirname "$(dirname "$manifest")")"): .NET $current -> $NET10_COMPAT_VERSION"
    perl -pi -e "s/\"\Q$current\E\"/\"$NET10_COMPAT_VERSION\"/g" "$manifest"
  fi
done

# Workloads land in ./.dotnet/sdk-manifests, ./.dotnet/packs and ./.dotnet/metadata: fully in-folder.
# --skip-manifest-update keeps the SDK's own (patched) manifests instead of fetching newer ones from the feeds;
# --source replaces the configured sources, so nuget.org has to be repeated.
"$DOTNET_DIR/dotnet" workload install maui --skip-manifest-update --skip-sign-check \
  --source "$DOTNET11_FEED" --source "$NUGET_ORG"
"$DOTNET_DIR/dotnet" workload list

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
echo
echo "Check the iOS workload's Xcode: grep -r '\"Xcode\"\\|XcodeVersion' $DOTNET_DIR/sdk-manifests/*/microsoft.net.sdk.ios/*/WorkloadManifest.json"
