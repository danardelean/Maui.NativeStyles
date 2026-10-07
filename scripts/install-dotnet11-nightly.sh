#!/usr/bin/env bash
# This repository's defaults for the in-folder nightly installer: the .NET 11 RC2 lane (iOS workload built for
# Xcode 27), the maui workload, the SDK in ./.dotnet at the repository root and a global.json next to it.
# Any option of install-dotnet-nightly.sh can be appended, e.g. --prune or --channel 11.0.1xx.
# install-dotnet-nightly.sh is a copy of install.sh from https://github.com/danardelean/dotnet-nightly-local.
set -euo pipefail
cd "$(dirname "$0")/.."
exec scripts/install-dotnet-nightly.sh --dir .dotnet --channel 11.0.1xx-rc2 --workloads maui "$@"
