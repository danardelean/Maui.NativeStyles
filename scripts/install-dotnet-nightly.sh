#!/usr/bin/env bash
# install-dotnet-nightly.sh: a .NET nightly SDK and any of its workloads in a folder of your choice.
#
# Nothing is written to the machine-wide .NET install: the SDK, its runtime, the workload manifests and packs all land
# in one folder (default ./.dotnet), and a global.json in the current directory makes the regular `dotnet` command
# pick that SDK up from there ("sdk.paths", a .NET 10 SDK host feature). Run it again to update: a newer daily is
# installed next to the old one, the manifests are refreshed, global.json is re-pinned (--prune drops the old SDKs).
# Remove the folder and global.json to undo.
#
# Builds (`dotnet build`, the IDE) find the SDK, the workload manifests and the packs through global.json. The
# `dotnet workload ...` commands do not: they look at the folder of the dotnet executable that runs them, so run them
# as DIR/dotnet workload list (or put DIR first on PATH).
#
# Usage: install-dotnet-nightly.sh [options]
#   -d, --dir DIR             install folder (default: ./.dotnet)
#   -c, --channel CHANNEL     daily-build channel, a release/* branch of dotnet/dotnet without the prefix (see
#                             --list-channels); required unless --version or --skip-sdk is given
#   -q, --quality QUALITY     daily | preview | ga (default: daily)
#   -v, --version VERSION     an exact SDK version instead of the newest build of the channel
#   -w, --workloads LIST      workloads to install, comma-separated: maui, ios, android, maui-android, wasm-tools,
#                             aspire ... (default: none, the SDK and the refreshed manifests only)
#   -b, --maui-branch BRANCH  dotnet/maui branch whose NuGet.config lists the feeds its build uses: release branches of
#                             dotnet/macios and dotnet/android publish to isolated darc-pub-* feeds, and that file names
#                             them. "auto" (default) derives it from the SDK band: release/<major>.0.1xx-<label> for
#                             a preview or rc band, net<major>.0 for a release band, main for an alpha; "none" to skip
#   -f, --feeds LIST          extra NuGet v3 feeds to search for manifests and packs, comma-separated
#       --no-default-feeds    do not search the dotnet<major> feed (pkgs.dev.azure.com/dnceng/public/_packaging/dotnetN)
#       --compat-runtime VER  runtime version for the previous major's compat manifests (default: newest on nuget.org)
#       --no-global-json      do not write global.json in the current directory
#       --skip-sdk            do not (re)install the SDK; use the newest one already in DIR
#       --manifests-only      stop after placing the manifests (no workload install)
#       --prune               remove older SDK, runtime and host versions from DIR
#       --list-channels       show the channels that currently have daily builds, with today's SDK version, and exit
#   -h, --help
#
# Requires: bash 3.2+, curl, unzip, perl. macOS or Linux (Apple workloads need macOS).
#
# How it works
#   1. SDK: the official dotnet-install script, --install-dir DIR, nothing on PATH.
#   2. Manifests: a daily SDK bundles stale baseline workload manifests (preview-era iOS / MAUI, in an older band folder
#      that it falls back to). For every manifest id the SDK lists (KnownWorkloadManifests.txt) the newest package of the
#      SDK's own version band (<id>.Manifest-<band>) is downloaded from the feeds and placed in sdk-manifests/<band>/,
#      what dotnet/maui's own build does. Builds of the band's own prerelease lane (its rc.N or preview.N label) win
#      over other lanes that publish into the band.
#   3. Compat manifests: the *-net<previous> workloads (pulled in by maui, ios, android, wasm-tools for apps on the
#      previous .NET) reference the previous runtime the SDK was built with, often a servicing release that reaches
#      nuget.org only on Patch Tuesday. They are pointed at the newest public one instead; they are not used to build
#      for the new .NET anyway.
#   4. Packs: `dotnet workload install` with --skip-manifest-update (exactly the manifests placed above) from the same
#      feeds plus nuget.org. `dotnet workload clean` drops packs of replaced manifests.
set -euo pipefail

INSTALL_SCRIPT_URL="https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh"
NUGET_ORG="https://api.nuget.org/v3/index.json"
DNCENG="https://pkgs.dev.azure.com/dnceng/public/_packaging"
BUILDS_TABLE_URL="https://raw.githubusercontent.com/dotnet/dotnet/main/docs/builds-table.md"
CURL=(curl -sSL --max-time 60)

log() { printf '%s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

# Version band of an SDK version: the patch number rounded down to the hundred and the preview/rc/alpha label kept,
# the build number and an rtm/servicing label dropped (X.Y.Zpp-<label>.N.<build> -> X.Y.Z00-<label>.N)
band_of() { echo "$1" | sed -E 's/^([0-9]+\.[0-9]+\.[0-9])[0-9][0-9](-(preview|rc|alpha)\.[0-9]+)?.*$/\100\2/'; }
# Prerelease label of a band (rc.N, preview.N, alpha.N), empty for a release band
label_of() { case "$1" in *-*) echo "${1#*-}" ;; *) echo "" ;; esac; }
major_of() { echo "${1%%.*}"; }
# The dotnet/maui branch that builds against a band: release/<major>.0.1xx-<label without the dot> for a preview or
# rc band, net<major>.0 for a release band, main for an alpha band
maui_branch_for() {
  local major label; major="$(major_of "$1")"; label="$(label_of "$1")"
  case "$label" in
    "") echo "net$major.0" ;;
    alpha.*) echo "main" ;;
    *) echo "release/$major.0.1xx-$(echo "$label" | tr -d .)" ;;
  esac
}

# NuGet v3 flat container: <feed>/flat2/<id>/index.json lists the versions, <feed>/flat2/<id>/<v>/<id>.<v>.nupkg is the package.
flat() {
  case "$1" in
    "$NUGET_ORG") echo "https://api.nuget.org/v3-flatcontainer" ;;
    *) echo "${1%/index.json}/flat2" ;;
  esac
}
versions() { "${CURL[@]}" -f "$(flat "$1")/$2/index.json" 2>/dev/null | tr -d ' \n' | grep -o '"versions":\[[^]]*\]' | grep -o '"[^"]*"' | tr -d '"' || true; }

# stdin: "version<TAB>anything" lines; stdout: the line with the highest version by Semantic Versioning precedence
# (numeric core, a release above its prereleases, prerelease fields compared one by one, numeric < alphanumeric).
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

# Newest released MAJOR.0.x runtime on nuget.org (Microsoft.NETCore.App.Ref)
newest_public_runtime() {
  versions "$NUGET_ORG" microsoft.netcore.app.ref | grep -E "^$1\.0\.[0-9]+\$" | sed 's/$/	/' | pick_highest | cut -f1
}

# The compat manifests of the previous major (*.net<PREV>) under DIR: point their <PREV>.0.x pack versions at VERSION
patch_compat_manifests() {
  local dir="${1:?}" prev="${2:?}" version="${3:?}" manifest current
  for manifest in "$dir"/sdk-manifests/*/microsoft.net.workload.mono.toolchain.net"$prev"/*/WorkloadManifest.json \
                  "$dir"/sdk-manifests/*/microsoft.net.workload.emscripten.net"$prev"/*/WorkloadManifest.json; do
    [ -f "$manifest" ] || continue
    current="$(grep -o "\"version\" *: *\"$prev\.0\.[0-9]*\"" "$manifest" | head -1 | grep -o "$prev\.0\.[0-9]*" || true)"
    if [ -n "$current" ] && [ "$current" != "$version" ]; then
      log "  compat manifest $(basename "$(dirname "$(dirname "$manifest")")"): .NET $current -> $version"
      perl -pi -e "s/\"\Q$current\E\"/\"$version\"/g" "$manifest"
    fi
  done
}

# The channels with daily builds: the ones the .NET builds table (dotnet/dotnet, docs/builds-table.md) links to as
# https://aka.ms/dotnet/<channel>/daily/..., one per actively built branch (main is its X.Y.1xx channel). Older release
# branches keep their channel but no longer get dailies. A channel's current SDK version is read the way dotnet-install
# does it: the aka.ms link of the SDK archive redirects to the build's own URL, which carries the version
# (.../Sdk/<version>/dotnet-sdk-<version>-<rid>.tar.gz); a HEAD request follows it without downloading. An unknown
# aka.ms path does not 404 but lands on a Microsoft search page, hence the pattern check.
channel_version() {
  local url
  url="$(curl -sSIL --max-time 60 -o /dev/null -w '%{url_effective}' "https://aka.ms/dotnet/$1/daily/dotnet-sdk-linux-x64.tar.gz" 2>/dev/null || true)"
  case "$url" in
    *dotnet-sdk-*-linux-x64.tar.gz) echo "$url" | sed -E 's#.*/dotnet-sdk-(.+)-linux-x64\.tar\.gz$#\1#' ;;
    *) echo "?" ;;
  esac
}
list_channels() {
  local channels ch
  channels="$("${CURL[@]}" -f "$BUILDS_TABLE_URL" | grep -o 'aka\.ms/dotnet/[^/)" ]*/daily' | sed 's#aka\.ms/dotnet/##; s#/daily##' | sort -u)"
  [ -n "$channels" ] || die "could not read the builds table at $BUILDS_TABLE_URL"
  printf '%-16s %s\n' "channel" "current daily SDK"
  for ch in $channels; do printf '%-16s %s\n' "$ch" "$(channel_version "$ch")"; done
}

# Highest version folder under DIR/<sub>
newest_dir() { ls -d "${1:?}"/*/ 2>/dev/null | xargs -n1 basename | sed 's/$/	/' | pick_highest | cut -f1; }

main() {
  local dir="./.dotnet" channel="" quality="daily" version="" workloads="" maui_branch="auto"
  local extra_feeds="" default_feeds=1 compat_runtime="" global_json=1 skip_sdk=0 manifests_only=0 prune=0
  while [ $# -gt 0 ]; do
    case "$1" in
      -d|--dir) dir="$2"; shift 2 ;;
      -c|--channel) channel="$2"; shift 2 ;;
      -q|--quality) quality="$2"; shift 2 ;;
      -v|--version) version="$2"; shift 2 ;;
      -w|--workloads) workloads="$2"; shift 2 ;;
      -b|--maui-branch) maui_branch="$2"; shift 2 ;;
      -f|--feeds) extra_feeds="$2"; shift 2 ;;
      --no-default-feeds) default_feeds=0; shift ;;
      --compat-runtime) compat_runtime="$2"; shift 2 ;;
      --no-global-json) global_json=0; shift ;;
      --skip-sdk) skip_sdk=1; shift ;;
      --manifests-only) manifests_only=1; shift ;;
      --prune) prune=1; shift ;;
      --list-channels) list_channels; return 0 ;;
      -h|--help) sed -n '2,/^set -euo/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//'; return 0 ;;
      *) die "unknown option $1 (see --help)" ;;
    esac
  done
  if [ "$skip_sdk" = 0 ] && [ -z "$channel" ] && [ -z "$version" ]; then
    die "choose a daily-build channel with --channel (or an exact SDK with --version). The channels with dailies: $0 --list-channels"
  fi
  mkdir -p "$dir"
  dir="$(cd "$dir" && pwd)"
  work="$(mktemp -d)"   # not local: the EXIT trap runs after main returns
  trap 'rm -rf "${work:?}"' EXIT
  for tool in curl unzip perl; do command -v "$tool" >/dev/null || die "$tool is required"; done

  # ---- 1. SDK ----------------------------------------------------------------------------------------------------------
  if [ "$skip_sdk" = 0 ]; then
    "${CURL[@]}" "$INSTALL_SCRIPT_URL" -o "$work/dotnet-install.sh"
    if [ -n "$version" ]; then
      # --version and --quality are mutually exclusive; dailies are served from ci.dot.net/public, one of the script's feeds
      bash "$work/dotnet-install.sh" --version "$version" --install-dir "$dir" --no-path
    else
      # resolves https://aka.ms/dotnet/<channel>/<quality>/dotnet-sdk-<os>-<arch>.tar.gz
      bash "$work/dotnet-install.sh" --channel "$channel" --quality "$quality" --install-dir "$dir" --no-path
    fi
  fi
  [ -x "$dir/dotnet" ] || die "no dotnet in $dir"
  # The newest SDK in the folder, from the folder itself: `dotnet --version` would obey a global.json pinned to an older one
  local sdk_version; sdk_version="$(newest_dir "$dir/sdk")"
  [ -n "$sdk_version" ] || die "no SDK in $dir/sdk"
  local band label major prev
  band="$(band_of "$sdk_version")"; label="$(label_of "$band")"; major="$(major_of "$sdk_version")"; prev=$((major - 1))
  local manifests_dir="$dir/sdk-manifests/$band"
  log "SDK $sdk_version (band $band) in $dir"
  mkdir -p "$manifests_dir"
  # dotnet commands run from an empty directory: a global.json up the tree would select another SDK
  local run_dir="$work/run"; mkdir -p "$run_dir"
  dotnet() { (cd "$run_dir" && DOTNET_ROOT="$dir" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 "$dir/dotnet" "$@"); }

  # ---- 2. Feeds --------------------------------------------------------------------------------------------------------
  local feeds=() f
  add_feed() { for f in "${feeds[@]+"${feeds[@]}"}"; do [ "$f" = "$1" ] && return 0; done; feeds+=("$1"); }
  [ "$default_feeds" = 1 ] && add_feed "$DNCENG/dotnet$major/nuget/v3/index.json"
  [ "$maui_branch" = "auto" ] && maui_branch="$(maui_branch_for "$band")"
  if [ "$maui_branch" != "none" ]; then
    for f in $("${CURL[@]}" -f "https://raw.githubusercontent.com/dotnet/maui/$maui_branch/NuGet.config" 2>/dev/null \
        | grep -o 'value="https://pkgs.dev.azure.com/dnceng/public/_packaging/[^"]*"' | cut -d'"' -f2 \
        | grep -E "/(dotnet$major[^/]*|darc-pub-[^/]*)/nuget/v3/index.json\$" || true); do add_feed "$f"; done
  fi
  for f in $(echo "$extra_feeds" | tr ',' ' '); do add_feed "$f"; done
  [ ${#feeds[@]} -gt 0 ] || die "no feeds to search (see --feeds)"
  log "Feeds:"; for f in "${feeds[@]}"; do log "  $f"; done

  # ---- 3. Manifests of this band -----------------------------------------------------------------------------------------
  local ids="$work/manifest-ids.txt"
  if [ -f "$dir/sdk/$sdk_version/KnownWorkloadManifests.txt" ]; then
    cp "$dir/sdk/$sdk_version/KnownWorkloadManifests.txt" "$ids"
  elif [ -f "$dir/sdk/$sdk_version/IncludedWorkloadManifests.txt" ]; then
    cp "$dir/sdk/$sdk_version/IncludedWorkloadManifests.txt" "$ids"
  else
    ls -d "$dir"/sdk-manifests/*/*/ | xargs -n1 basename | grep -v '^workloadsets$' | sort -u > "$ids"
  fi
  log "Manifests ($band):"
  local id pkg v chosen best best_feed have target
  while IFS= read -r id || [ -n "$id" ]; do
    id="$(echo "$id" | tr -d '\r' | tr '[:upper:]' '[:lower:]')"
    [ -z "$id" ] && continue
    pkg="$id.manifest-$band"
    : > "$work/candidates"
    for f in "${feeds[@]}"; do
      for v in $(versions "$f" "$pkg"); do printf '%s\t%s\n' "$v" "$f" >> "$work/candidates"; done
    done
    # builds of this band's own prerelease lane first: other lanes publish into the band too, and a preview build from
    # main can outrank the band's rc build by version alone
    if [ -n "$label" ] && grep -q -F -- "$label" "$work/candidates"; then
      grep -F -- "$label" "$work/candidates" > "$work/candidates.lane" && mv "$work/candidates.lane" "$work/candidates"
    fi
    chosen="$(pick_highest < "$work/candidates")"
    best="${chosen%%	*}"; best_feed="${chosen#*	}"
    if [ -z "$best" ]; then
      have="$(ls -d "$dir"/sdk-manifests/*/"$id"/*/ 2>/dev/null | tail -1 || true)"
      log "  $id: no $pkg on any feed, keeping the SDK's ${have:-<none>}"
      continue
    fi
    target="${manifests_dir:?}/${id:?}"
    if [ -d "$target/$best" ]; then
      log "  $id: $best (present)"
      continue
    fi
    log "  $id: $best  ($(echo "$best_feed" | sed -E 's#.*/_packaging/([^/]*)/.*#\1#; s#https://api.nuget.org.*#nuget.org#'))"
    "${CURL[@]}" -f "$(flat "$best_feed")/$pkg/$best/$pkg.$best.nupkg" -o "$work/$pkg.nupkg"
    rm -rf "${work:?}/data" && unzip -q -o "$work/$pkg.nupkg" 'data/*' -d "$work"
    rm -rf "${target:?}" && mkdir -p "$target/$best" && cp -R "$work/data/." "$target/$best/"
  done < "$ids"
  # manifest versions pinned by an earlier install would override the folders above
  rm -f "$dir/metadata/workloads/$band/InstallState/default.json"

  # ---- 4. Compat manifests of the previous major ---------------------------------------------------------------------------
  [ -n "$compat_runtime" ] || compat_runtime="$(newest_public_runtime "$prev")"
  if [ -n "$compat_runtime" ]; then
    patch_compat_manifests "$dir" "$prev" "$compat_runtime"
  else
    log "  (no public .NET $prev runtime found on nuget.org: compat manifests left as they are)"
  fi

  # ---- 5. Packs ----------------------------------------------------------------------------------------------------------
  local sources=()
  for f in "${feeds[@]}" "$NUGET_ORG"; do sources+=(--source "$f"); done
  if [ "$manifests_only" = 1 ] || [ -z "$workloads" ]; then
    [ -n "$workloads" ] || log "No workloads requested (-w). Later: $dir/dotnet workload install <id> --skip-manifest-update --skip-sign-check ${sources[*]}"
  else
    # shellcheck disable=SC2046
    dotnet workload install $(echo "$workloads" | tr ',' ' ') --skip-manifest-update --skip-sign-check "${sources[@]}"
    dotnet workload clean >/dev/null 2>&1 || true
    dotnet workload list
  fi

  # ---- 6. Prune ----------------------------------------------------------------------------------------------------------
  if [ "$prune" = 1 ]; then
    local sub keep d
    for sub in sdk shared/Microsoft.NETCore.App shared/Microsoft.AspNetCore.App host/fxr; do
      [ -d "$dir/$sub" ] || continue
      keep="$(newest_dir "$dir/$sub")"
      for d in "${dir:?}/${sub:?}"/*/; do
        [ "$(basename "$d")" = "$keep" ] || { log "  pruning $sub/$(basename "$d")"; rm -rf "${d:?}"; }
      done
    done
  fi

  # ---- 7. global.json ----------------------------------------------------------------------------------------------------
  if [ "$global_json" = 1 ]; then
    local rel="$dir"
    case "$dir" in "$PWD"/*) rel="${dir#"$PWD"/}" ;; esac
    cat > global.json <<JSON
{
  "sdk": {
    "version": "$sdk_version",
    "paths": [ "$rel", "\$host\$" ],
    "errorMessage": "The .NET SDK $sdk_version was not found in $rel: run the install script again."
  }
}
JSON
    log "global.json: SDK $sdk_version from $rel"
  fi
  log "Manifests in $manifests_dir:"
  for d in "$manifests_dir"/*/; do [ "$(basename "$d")" = "workloadsets" ] || log "  $(basename "$d"): $(ls "$d" | tr '\n' ' ')"; done
  log "Workload commands look at the folder of the dotnet that runs them: use $dir/dotnet workload list (builds need nothing)."
}

if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi
