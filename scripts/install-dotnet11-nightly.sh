#!/usr/bin/env bash
# Installs a .NET 11 nightly SDK into ./.dotnet (repository root) and the maui workload into that same folder.
# Nothing is written to /usr/local/share/dotnet. Remove ./.dotnet and ./global.json to undo.
#
#   ./scripts/install-dotnet11-nightly.sh                      # newest daily of the RC2 lane (iOS workload built for Xcode 27)
#   DOTNET_VERSION=11.0.100-rc.2.26504.105 ./scripts/...       # a specific daily (the one dotnet/maui's RC2 branch uses)
#   DOTNET_CHANNEL=11.0.1xx ./scripts/...                      # main (RTM) lane instead of RC2
#   WORKLOAD_ROLLBACK=.github/workload-versions-net11.json ... # pin the workload manifests
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

# Workloads land in ./.dotnet/sdk-manifests, ./.dotnet/packs and ./.dotnet/metadata: fully in-folder.
# --source replaces the configured sources, so nuget.org has to be repeated.
if [ -n "${WORKLOAD_ROLLBACK:-}" ]; then
  "$DOTNET_DIR/dotnet" workload install maui --skip-sign-check --from-rollback-file "$WORKLOAD_ROLLBACK" \
    --source "$DOTNET11_FEED" --source "$NUGET_ORG"
else
  "$DOTNET_DIR/dotnet" workload install maui --skip-sign-check --source "$DOTNET11_FEED" --source "$NUGET_ORG"
fi
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
