#!/usr/bin/env bash
# Tests for install-dotnet-nightly.sh that need no Microsoft download: the pure functions, then end-to-end runs on a
# fake SDK folder (a fake `dotnet` that logs its calls) with the real RC1-band manifests from nuget.org, and the feed
# discovery from dotnet/maui's NuGet.config. Needs curl, unzip, perl, python3 and access to nuget.org and GitHub.
#   scripts/test-install-dotnet-nightly.sh
set -uo pipefail
SCRIPT="${1:-$(dirname "$0")/install-dotnet-nightly.sh}"
SCRIPT="$(cd "$(dirname "$SCRIPT")" && pwd)/$(basename "$SCRIPT")"
pass=0; fail=0
ok()   { pass=$((pass+1)); printf '  ok   %s\n' "$1"; }
bad()  { fail=$((fail+1)); printf '  FAIL %s\n       got: %s\n' "$1" "$2"; }
check() { # name expected actual
  if [ "$2" = "$3" ]; then ok "$1"; else bad "$1 (expected: $2)" "$3"; fi
}

echo "== functions"
# shellcheck source=/dev/null
source "$SCRIPT"; set +e   # the script sets -e when sourced
check "band rc.2"        "11.0.100-rc.2"      "$(band_of 11.0.100-rc.2.26504.105)"
check "band rtm"         "11.0.100"           "$(band_of 11.0.100-rtm.26480.113)"
check "band preview"     "11.0.100-preview.7" "$(band_of 11.0.100-preview.7.26380.1)"
check "band alpha 12"    "12.0.100-alpha.1"   "$(band_of 12.0.100-alpha.1.26480.102)"
check "band release"     "11.0.100"           "$(band_of 11.0.100)"
check "band 2xx"         "11.0.200-rc.1"      "$(band_of 11.0.201-rc.1.26500.1)"
check "label rc.2"       "rc.2"               "$(label_of 11.0.100-rc.2)"
check "label none"       ""                   "$(label_of 11.0.100)"
check "major"            "11"                 "$(major_of 11.0.100-rc.2.26504.105)"
check "flat nuget.org"   "https://api.nuget.org/v3-flatcontainer" "$(flat https://api.nuget.org/v3/index.json)"
check "flat dnceng"      "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/flat2" "$(flat https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet11/nuget/v3/index.json)"
check "pick rc over preview" "11.0.0-rc.2.26504.105	B" "$(printf '11.0.0-preview.7.26471.7\tA\n11.0.0-rc.2.26504.105\tB\n11.0.0-rc.2.26480.1\tC\n' | pick_highest)"
check "pick core"        "27.0.12211-net11-rc.2	B" "$(printf '26.5.12253-net11-rc.2\tA\n27.0.12211-net11-rc.2\tB\n27.0.12195-net11-rc.2\tC\n' | pick_highest)"
check "pick release > rc" "11.0.0	B" "$(printf '11.0.0-rc.2.26504.105\tA\n11.0.0\tB\n' | pick_highest)"
check "pick build number" "11.0.100-rc.2.26502.119	B" "$(printf '11.0.100-rc.2.26425.128\tA\n11.0.100-rc.2.26502.119\tB\n' | pick_highest)"
check "pick empty"       ""                   "$(printf '' | pick_highest)"
check "newest public .NET 10 runtime (nuget.org)" "10.0." "$(newest_public_runtime 10 | cut -c1-5)"
check "newest public .NET 10 runtime is a release" "" "$(newest_public_runtime 10 | grep -- - || true)"

echo "== compat manifest patch"
t="$(mktemp -d)"
mkdir -p "$t/sdk-manifests/11.0.100-rc.2/microsoft.net.workload.mono.toolchain.net10/11.0.100-rc.2.1"
cat > "$t/sdk-manifests/11.0.100-rc.2/microsoft.net.workload.mono.toolchain.net10/11.0.100-rc.2.1/WorkloadManifest.json" <<'JSON'
{ "version": "10.0.99", "packs": { "Microsoft.NETCore.App.Runtime.Mono.android-x64": { "kind": "framework", "version": "10.0.99" }, "Microsoft.NET.Runtime.MonoAOTCompiler.Task": { "kind": "Sdk", "version": "10.0.99" } } }
JSON
out="$(patch_compat_manifests "$t" 10 10.0.12)"
check "patch logs"       "  compat manifest microsoft.net.workload.mono.toolchain.net10: .NET 10.0.99 -> 10.0.12" "$out"
check "patch applied"    "0" "$(grep -c 10.0.99 "$t/sdk-manifests/11.0.100-rc.2/microsoft.net.workload.mono.toolchain.net10/11.0.100-rc.2.1/WorkloadManifest.json")"
check "patch count"      "3" "$(grep -o 10.0.12 "$t/sdk-manifests/11.0.100-rc.2/microsoft.net.workload.mono.toolchain.net10/11.0.100-rc.2.1/WorkloadManifest.json" | wc -l | tr -d ' ')"
out="$(patch_compat_manifests "$t" 10 10.0.12)"
check "patch idempotent" "" "$out"
rm -rf "${t:?}"

echo "== end to end on a fake SDK folder (manifests from nuget.org, RC1 band)"
root="$(mktemp -d)"; cd "$root"
SDKV="11.0.100-rc.1.26451.107"
mkdir -p ".dotnet/sdk/$SDKV" .dotnet/sdk/11.0.100-preview.7.26380.1 .dotnet/shared/Microsoft.NETCore.App/11.0.0-rc.1.1 .dotnet/shared/Microsoft.NETCore.App/11.0.0-preview.7.1 .dotnet/host/fxr/11.0.0-rc.1.1
printf 'Microsoft.NET.Sdk.Android\nMicrosoft.NET.Sdk.iOS\nMicrosoft.NET.Sdk.Maui\nMicrosoft.NET.Workload.Mono.ToolChain.Current\nMicrosoft.NET.Workload.Mono.ToolChain.net10\nnot.a.real.manifest\n' > ".dotnet/sdk/$SDKV/KnownWorkloadManifests.txt"
# stale baseline manifest in an older band folder, as a daily SDK ships it
mkdir -p .dotnet/sdk-manifests/11.0.100-preview.6/microsoft.net.sdk.ios/26.5.11720-net11-p6
echo '{ "version": "26.5.11720-net11-p6" }' > .dotnet/sdk-manifests/11.0.100-preview.6/microsoft.net.sdk.ios/26.5.11720-net11-p6/WorkloadManifest.json
# a pin left by an earlier install
mkdir -p .dotnet/metadata/workloads/11.0.100-rc.1/InstallState && echo '{}' > .dotnet/metadata/workloads/11.0.100-rc.1/InstallState/default.json
# fake dotnet: logs every call, answers `workload list`
cat > .dotnet/dotnet <<'SH'
#!/usr/bin/env bash
echo "$PWD :: $*" >> "$FAKE_LOG"
case "$1 $2" in "workload list") echo "Installed Workload Id    Manifest Version"; echo "maui-android             fake" ;; esac
SH
chmod +x .dotnet/dotnet
export FAKE_LOG="$root/dotnet-calls.log"; : > "$FAKE_LOG"
# an old global.json pinning another version, which must not disturb the run
echo '{ "sdk": { "version": "11.0.100-preview.7.26380.1", "paths": [ ".dotnet", "$host$" ] } }' > global.json

run1="$("$SCRIPT" --skip-sdk --no-default-feeds --maui-branch none --feeds https://api.nuget.org/v3/index.json --workloads android,maui-android 2>&1)"; rc=$?
echo "$run1" | sed 's/^/     | /'
check "exit code"            "0" "$rc"
check "SDK picked (newest in folder, not the pinned one)" "SDK $SDKV (band 11.0.100-rc.1) in $root/.dotnet" "$(echo "$run1" | grep '^SDK ')"
check "ios manifest placed"  "26.5.12194-net11-rc.1" "$(ls .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.sdk.ios)"
check "ios manifest files"   "WorkloadManifest.json WorkloadManifest.targets" "$(ls .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.sdk.ios/*/ | grep -v Dependencies | tr '\n' ' ' | sed 's/ $//')"
check "android manifest placed" "37.0.0-rc.1.2257" "$(ls .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.sdk.android)"
check "maui manifest placed" "11.0.0-rc.1.26451.6" "$(ls .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.sdk.maui)"
check "toolchain manifest placed" "11.0.100-rc.1.26425.128" "$(ls .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.workload.mono.toolchain.current)"
check "unknown manifest reported" "1" "$(echo "$run1" | grep -c 'not.a.real.manifest: no not.a.real.manifest.manifest-11.0.100-rc.1 on any feed')"
check "stale baseline untouched" "26.5.11720-net11-p6" "$(ls .dotnet/sdk-manifests/11.0.100-preview.6/microsoft.net.sdk.ios)"
check "install state pin removed" "0" "$(ls .dotnet/metadata/workloads/11.0.100-rc.1/InstallState 2>/dev/null | wc -l | tr -d ' ')"
check "net10 compat manifest is public already (no patch line)" "0" "$(echo "$run1" | grep -c 'compat manifest')"
check "net10 compat packs all at the newest public runtime" "$(newest_public_runtime 10)" "$(python3 -I -c 'import json,sys; print(" ".join(sorted({p["version"] for p in json.load(open(sys.argv[1]))["packs"].values()})))' .dotnet/sdk-manifests/11.0.100-rc.1/microsoft.net.workload.mono.toolchain.net10/*/WorkloadManifest.json)"
check "workload install call" "workload install android maui-android --skip-manifest-update --skip-sign-check --source https://api.nuget.org/v3/index.json --source https://api.nuget.org/v3/index.json" "$(grep -o 'workload install.*' "$FAKE_LOG")"
check "dotnet ran outside the repo dir" "0" "$(grep -c "^$root ::" "$FAKE_LOG")"
check "workload clean + list called" "2" "$(grep -c 'workload clean\|workload list' "$FAKE_LOG")"
check "global.json re-pinned" "$SDKV" "$(grep -o '11\.0\.100[^"]*' global.json | head -1)"
check "global.json relative path" '"paths": [ ".dotnet", "$host$" ],' "$(grep paths global.json | sed 's/^ *//')"
check "old SDK not pruned without --prune" "2" "$(ls .dotnet/sdk | wc -l | tr -d ' ')"
check "workload command hint" "1" "$(echo "$run1" | grep -c "use $root/.dotnet/dotnet workload list")"

echo "== second run: idempotent, --manifests-only, --no-global-json, --prune"
rm -f global.json; : > "$FAKE_LOG"
run2="$("$SCRIPT" --skip-sdk --no-default-feeds --maui-branch none --feeds https://api.nuget.org/v3/index.json --manifests-only --no-global-json --prune 2>&1)"; rc=$?
check "exit code"            "0" "$rc"
check "manifests present, not re-downloaded" "5" "$(echo "$run2" | grep -c '(present)')"
check "no dotnet call"       "0" "$(wc -l < "$FAKE_LOG" | tr -d ' ')"
check "no global.json"       "" "$(ls global.json 2>/dev/null)"
check "pruned old sdk"       "$SDKV" "$(ls .dotnet/sdk)"
check "pruned old runtime"   "11.0.0-rc.1.1" "$(ls .dotnet/shared/Microsoft.NETCore.App)"
check "prune logged"         "2" "$(echo "$run2" | grep -c pruning)"

echo "== --dir outside cwd, --compat-runtime override"
other="$(mktemp -d)"; mkdir -p "$other/sdk/$SDKV" && cp .dotnet/dotnet "$other/dotnet" && printf 'Microsoft.NET.Workload.Mono.ToolChain.net10\n' > "$other/sdk/$SDKV/KnownWorkloadManifests.txt"
run3="$("$SCRIPT" --skip-sdk --dir "$other" --no-default-feeds --maui-branch none --feeds https://api.nuget.org/v3/index.json --manifests-only --compat-runtime 10.0.5 2>&1)"; rc=$?
check "exit code"            "0" "$rc"
check "compat override applied" "1" "$(echo "$run3" | grep -c -- '-> 10.0.5')"
check "global.json absolute path" "\"paths\": [ \"$other\", \"\$host\$\" ]," "$(grep paths global.json | sed 's/^ *//')"
rm -rf "${other:?}"

echo "== feeds from dotnet/maui's RC2 NuGet.config (network: GitHub)"
run4="$("$SCRIPT" --skip-sdk --no-default-feeds --manifests-only --no-global-json 2>&1 | sed -n '/^Feeds:/,/^Manifests (/p')"
echo "$run4" | sed 's/^/     | /'
check "darc-pub macios feed found" "1" "$(echo "$run4" | grep -c 'darc-pub-dotnet-macios')"
check "dotnet11 feed found via NuGet.config" "1" "$(echo "$run4" | grep -c '/dotnet11/nuget')"
check "no dotnet10 feed" "0" "$(echo "$run4" | grep -c '/dotnet10')"

cd /; rm -rf "${root:?}"
echo; echo "passed: $pass  failed: $fail"
[ "$fail" = 0 ]
