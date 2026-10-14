#!/usr/bin/env bash
# Publish the three NuGet packages and the shared TypeScript package from this
# machine with the keys registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            build, pack exactly four artifacts, publish, tag v<x.y.z>, GitHub release;
#                                   returns once NuGet and npm accepted the push and the tag and the GitHub
#                                   release exist, and leaves the verification below running detached
#   ./release.sh <x.y.z> --dry-run  every check, the real build and pack, a NuGet key probe for every package id
#                                   and npm's publish --dry-run; nothing is pushed or published. Checks the real
#                                   run enforces (tag, already on NuGet or npm) only warn. Prints what the
#                                   detached job would do.
#   ./release.sh --wait <x.y.z>     blocks until artifacts/release-<x.y.z>-verify.log ends in VERIFIED (exit 0)
#                                   or FAILED (exit 1; exit 3 when not indexed in time), printing the log
#   ./release.sh --verify <x.y.z>   starts the detached verification again for a released version
#
# The detached verification (own session, survives the caller's shell) runs
# `./build.sh VerifyRelease --published-version <x.y.z>` (waits until nuget.org and npm serve all four
# packages, up to 45 min, then the release smoke against the registries: eng/release/verify-packages.sh
# and verify-typescript.sh), then qyl.at's scripts/refresh-published-versions.sh, and ends its log with
# VERIFIED or FAILED. Registry versions are immutable: a failed smoke after the fact is a patch release,
# and the log says so.
#
# To resume a partial release, preserve its artifacts and record, then use
# QYL_RELEASE_ARTIFACTS=/path/to/original/artifacts ./release.sh <version> --resume [--dry-run].
# Resume never rebuilds or re-uploads NuGet; it compares every public NuGet payload first.
#
# Version sources that must already agree on HEAD: Directory.Build.props VersionPrefix,
# registry/manifest.yaml schema_url (https://qyl.at/schemas/<x.y.z>) and a CHANGELOG.md
# "## [x.y.z]" entry, with scripts/generate.sh run. qyl.at must serve the schema URL before
# the real push (SKILL.md). Both registries receive the same version.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NUGET_GALLERY=https://www.nuget.org/api/v2
NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
NPM_REGISTRY=https://registry.npmjs.org
NPM_PACKAGE=@ancplua/qyl-semantic-conventions
NPM_TOKEN_FILE=$HOME/.config/vitals/npm-qyl-manual-publish-token
OUT=${QYL_RELEASE_ARTIFACTS:-"$PWD/artifacts"}
OUT=$(python3 -c 'from pathlib import Path; import sys; print(Path(sys.argv[1]).resolve())' "$OUT")
PACKAGES=(Qyl.Telemetry.SemanticConventions Qyl.Telemetry.SemanticConventions.Analyzers Qyl.Telemetry.SemanticConventions.Incubating)
QYL_AT_DIR=${QYL_AT_DIR:-$(cd .. && pwd)/qyl.at}

RELEASE_NAME="Qyl.Telemetry.SemanticConventions (3 NuGet + npm)"
VERIFY_COMMAND="./build.sh VerifyRelease"
VERIFY_WHAT="nuget.org and npm index up to 45 min, then the release smoke against the registries"

mode=release
case ${1:-} in --wait | --verify | __verify) mode=${1#--}; shift ;; esac
version=${1:-}
dry=0; resume=0; browser_auth=0
if [[ $mode == release ]]; then
  for flag in "${@:2}"; do
    case "$flag" in
      --dry-run) dry=1 ;;
      --resume) resume=1 ;;
      --browser-auth) browser_auth=1 ;;
      *) echo "unknown option: $flag" >&2; exit 2 ;;
    esac
  done
fi
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <x.y.z> [--dry-run] [--resume] [--browser-auth] | --wait <x.y.z> | --verify <x.y.z>" >&2; exit 2; }
say() { printf '\n== %s\n' "$*"; }
die() { echo "release: $*" >&2; exit 1; }
utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }
verify_log="$PWD/artifacts/release-$version-verify.log"
index_status="$PWD/artifacts/release-$version-index.status"  # "waiting|indexed|timed-out elapsed=<s>s at=<utc>"
verify_pid() { sed -n '1s/.* pid \([0-9][0-9]*\) .*/\1/p' "$verify_log"; }
index_state() { [[ -f $index_status ]] && cut -d' ' -f1 "$index_status"; }
index_elapsed() { sed -n 's/.* elapsed=\([^ ]*\) .*/\1/p' "$index_status" 2>/dev/null; }

# The detached job: VerifyRelease (index wait, then the registry consumers), then the snapshot
# refresh. Its first log line names its pid, its last line is the verdict; exit 3 = not indexed in time.
verify_published() {
  local step="start" next_patch="${version%.*}.$(( ${version##*.} + 1 ))"
  printf 'verify %s %s pid %s started %s\n' "$RELEASE_NAME" "$version" "$$" "$(utc)"
  trap 'rc=$?; (( rc == 0 )) || printf "FAILED %s %s %s: %s stopped (exit %s); fix the cause, then ./release.sh --verify %s\n" "$RELEASE_NAME" "$version" "$(utc)" "$step" "$rc" "$version"' EXIT
  fail() { local code=$1; shift; printf 'FAILED %s %s %s: %s\n' "$RELEASE_NAME" "$version" "$(utc)" "$*"; trap - EXIT; exit "$code"; }
  rm -f "$index_status"

  step="VerifyRelease"
  say "$VERIFY_COMMAND --published-version $version ($VERIFY_WHAT)"
  if ! $VERIFY_COMMAND --published-version "$version"; then
    [[ $(index_state) == timed-out ]] &&
      fail 3 "not indexed after $(index_elapsed); nothing is wrong with the release yet: ./release.sh --verify $version checks again"
    fail 1 "a published package failed VerifyRelease (above; full log under artifacts/logs/); $version is immutable: fix the cause and ship $next_patch as a patch release (a transient network failure: ./release.sh --verify $version)"
  fi

  step="snapshot refresh"
  [[ $(index_state) == indexed ]] || fail 1 "VerifyRelease passed but $index_status does not say indexed; the snapshot stays as it is"
  say "refresh the published-versions snapshot in qyl.at"
  "$QYL_AT_DIR/scripts/refresh-published-versions.sh" ||
    fail 1 "$version is published and verified, but the snapshot refresh failed (above); ./release.sh --verify $version retries it"

  trap - EXIT
  printf 'VERIFIED %s %s %s: indexed after %s, registry consumers passed, snapshot refreshed\n' "$RELEASE_NAME" "$version" "$(utc)" "$(index_elapsed)"
}

# A new session (python3's setsid; macOS has no setsid command) without a controlling terminal: neither
# the end of the caller's shell nor a signal to its process group reaches the job. SIGINT, SIGQUIT and
# SIGHUP go back to their defaults first: `&` ignores SIGINT and SIGQUIT for the child, an ignored
# signal stays ignored across exec, and a smoke that stops its tool with Ctrl-C (qyl's) would hang.
# The job needs no registry credential.
start_verification() {
  local pid
  mkdir -p artifacts
  if [[ -f $verify_log ]] && ! tail -n 1 "$verify_log" | grep -Eq '^(VERIFIED|FAILED) '; then
    pid=$(verify_pid)
    [[ -n $pid ]] && kill -0 "$pid" 2>/dev/null && die "the verification of $version is still running (pid $pid): ./release.sh --wait $version"
  fi
  : >"$verify_log"
  env -u NPM_CONFIG_USERCONFIG -u NPM_CONFIG_TOKEN python3 -I -c 'import os, signal, sys
os.setsid()
for number in (signal.SIGINT, signal.SIGQUIT, signal.SIGHUP): signal.signal(number, signal.SIG_DFL)
os.execv(sys.argv[1], sys.argv[1:])' \
    "$PWD/release.sh" __verify "$version" </dev/null >>"$verify_log" 2>&1 &
  disown
  # Return only once the job has logged its pid, so a --wait right after this finds it.
  for _ in $(seq 1 50); do [[ -n $(verify_pid) ]] && break; sleep 0.2; done
  [[ -n $(verify_pid) ]] || die "the detached verification did not start; read $verify_log"
  say "verification runs detached (pid $!)"
  echo "log:  $verify_log"
  echo "wait: ./release.sh --wait $version   (or: tail -f $verify_log)"
}

wait_for_verification() {
  local printed=0 lines pid alive
  [[ -f $verify_log ]] || die "no verification log for $version at $verify_log; ./release.sh --verify $version starts one"
  while :; do
    pid=$(verify_pid); alive=0
    [[ -n $pid ]] && kill -0 "$pid" 2>/dev/null && alive=1
    lines=$(wc -l <"$verify_log" | tr -d ' ')
    (( lines > printed )) && { sed -n "$((printed + 1)),${lines}p" "$verify_log"; printed=$lines; }
    case $(tail -n 1 "$verify_log") in
      VERIFIED\ *) return 0 ;;
      FAILED\ *) [[ $(index_state) == timed-out ]] && return 3; return 1 ;;
    esac
    (( alive )) || die "the verification job (pid ${pid:-unknown}) ended without VERIFIED or FAILED; read $verify_log, then ./release.sh --verify $version"
    sleep 5
  done
}

case $mode in
  __verify) verify_published; exit 0 ;;
  wait) wait_for_verification; exit ;;
  verify)
    [[ -n $(git ls-remote --tags origin "refs/tags/v$version") ]] || die "tag v$version is not on origin; release it first"
    gh release view "v$version" >/dev/null 2>&1 || die "GitHub release v$version does not exist; release it first"
    start_verification
    exit 0 ;;
esac

publish_npm() {
  if (( browser_auth )); then
    python3 eng/release/publish-npm.py --browser-auth "$@"
  else
    python3 eng/release/publish-npm.py "$@"
  fi
}
lower() { tr '[:upper:]' '[:lower:]' <<<"$1"; }
indexed() { curl -fsSL "$NUGET_FLAT/$(lower "$1")/index.json" 2>/dev/null | grep -q "\"$2\""; }
latest() { curl -fsSL "$NUGET_FLAT/$(lower "$1")/index.json" 2>/dev/null | grep -o '"[0-9][^"]*"' | tail -1 | tr -d '"'; }
# The key probe publishes nothing: it is NuGet's push-symbols handshake. The key mints a one-use
# package verification key for <id> <version> (only a live key with push scope can), and that key
# verifies with 200 only when the key's account owns <id>; the verify call consumes it. The key
# travels in a curl config on stdin, never on a command line.
key_probe() {
  local id=$1 version=$2 minted temp
  minted=$(printf 'header = "X-NuGet-ApiKey: %s"\n' "$(<"$NUGET_KEY_FILE")" |
    curl -fsS -X POST -H 'Content-Length: 0' -K - "$NUGET_GALLERY/package/create-verification-key/$id/$version") || return 1
  temp=$(sed -n 's/.*"Key":"\([^"]*\)".*/\1/p' <<<"$minted")
  [[ -n $temp ]] || return 1
  [[ $(printf 'header = "X-NuGet-ApiKey: %s"\n' "$temp" |
    curl -sS -o /dev/null -w '%{http_code}' -K - "$NUGET_GALLERY/verifykey/$id/$version") == 200 ]]
}

say "checks"
[[ -r $NUGET_KEY_FILE ]] || die "NuGet key missing: $NUGET_KEY_FILE (vitals keys)"
[[ -r $NPM_TOKEN_FILE ]] || die "npm token missing: $NPM_TOKEN_FILE (vitals keys)"
notes=$(mktemp); npmrc=$(mktemp)
chmod 600 "$npmrc"
trap 'rm -f "$notes" "$npmrc"' EXIT
printf '//registry.npmjs.org/:_authToken=%s\n' "$(<"$NPM_TOKEN_FILE")" >"$npmrc"
NPM_CONFIG_USERCONFIG=$npmrc npm whoami --registry="$NPM_REGISTRY" >/dev/null || die "npm token rejected (vitals keys); renew it before publication"
[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "working tree not clean"
git fetch -q origin main
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main differs from origin/main"
workflow_commit=$(git rev-parse HEAD)
release_commit=$workflow_commit
if (( resume )); then
  release_commit=$(python3 eng/release/artifact-record.py commit "$OUT" "$version")
  git cat-file -e "$release_commit^{commit}"
  git merge-base --is-ancestor "$release_commit" HEAD || die "recorded release commit is not an ancestor of main"
  git diff --exit-code "$release_commit" -- . \
    ":(exclude)AGENTS.md" ":(exclude)SKILL.md" ":(exclude)release.sh" \
    ":(exclude)eng/release" ":(exclude)tests/scripts/test_release_resume.py" || die "package sources differ from the original packed commit"
fi
if [[ -n $(git ls-remote --tags origin "refs/tags/v$version") ]] || git rev-parse -q --verify "refs/tags/v$version" >/dev/null; then
  (( dry )) || die "tag v$version already exists"; echo "warning: tag v$version already exists; the real run refuses"
fi
prefix=$(sed -n 's:.*<VersionPrefix[^>]*>\([^<]*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -1)
schema=$(sed -n 's#^schema_url: https://qyl.at/schemas/##p' registry/manifest.yaml)
[[ $prefix == "$version" ]] || die "Directory.Build.props VersionPrefix is $prefix, not $version"
npm_version=$(node -p "require('./typescript/package.json').version")
[[ $npm_version == "$version" ]] || die "typescript/package.json version is $npm_version, not $version"
[[ $schema == "$version" ]] || die "registry/manifest.yaml schema_url names $schema, not $version"
grep -q "^## \[$version\]" CHANGELOG.md || die "CHANGELOG.md has no '## [$version]' entry"
for id in "${PACKAGES[@]}"; do
  if indexed "$id" "$version"; then
    if (( ! resume )); then
      (( dry )) || die "$id $version is already on NuGet; use --resume with the original recorded artifacts"
      echo "warning: $id $version is already on NuGet; the real run refuses"
    fi
  elif (( resume )); then
    die "$id $version is not indexed yet; wait before resuming"
  fi
done
if npm view "$NPM_PACKAGE@$version" version --registry="$NPM_REGISTRY" >/dev/null 2>&1; then
  (( dry )) || die "$NPM_PACKAGE $version is already on npm"
  echo "warning: $NPM_PACKAGE $version is already on npm; the real run refuses"
fi
# The schema must be served before the NuGet push. Cloudflare's edge can answer 404 for a
# minute after a deploy, so poll instead of failing on the first miss; and when qyl.at has no
# file for this version yet, serve it from the local clone instead of stopping for a relay.
schema_url="https://qyl.at/schemas/$version"
schema_served() { [[ $(curl -fsS -o /dev/null -w '%{http_code}' "$schema_url" 2>/dev/null) == 200 ]]; }
poll_schema() { local i; for ((i = 0; i < $1; i++)); do schema_served && return 0; sleep 5; done; return 1; }
if poll_schema 2; then
  echo "qyl.at serves $schema_url"
else
  qyl_at=$QYL_AT_DIR
  if (( dry )); then
    echo "warning: $schema_url is not served yet; the real run adds public/schemas/$version in $qyl_at and deploys it"
  else
    [[ -d $qyl_at/public/schemas ]] || die "$schema_url is not served and no qyl.at clone at $qyl_at (set QYL_AT_DIR)"
    say "serve $schema_url from $qyl_at"
    (
      cd "$qyl_at"
      git pull -q --ff-only origin main
      [[ -z $(git status --porcelain --untracked-files=no) ]] || die "qyl.at working tree not clean"
      if [[ ! -f public/schemas/$version ]]; then
        latest=$(ls public/schemas | sort -V | tail -1)
        sed -e "s|^schema_url: https://qyl.at/schemas/.*|schema_url: $schema_url|" \
            -e "s|^versions:\$|versions:\n  $version:|" "public/schemas/$latest" > "public/schemas/$version"
        bun run check >/dev/null
        git add "public/schemas/$version"
        git commit -q -m "chore: serve schema $version" -m "Qyl.Telemetry.SemanticConventions $version needs $schema_url before its NuGet push." \
          -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
        git push -q origin main
      fi
      ./release.sh
    )
    poll_schema 36 || die "$schema_url still not served three minutes after the qyl.at deploy"
    echo "qyl.at serves $schema_url"
  fi
fi
gh auth status >/dev/null 2>&1 || die "gh is not logged in"
slnx=$(find . -maxdepth 1 -type f \( -name '*.slnx' -o -name '*.sln' \) -print -quit | sed 's#^\./##')
[[ -n $slnx ]] || die "no .slnx or .sln at the repo root"

if (( resume )); then
  say "verify preserved artifacts and every existing NuGet payload"
  python3 eng/release/verify-artifact-set.py "$OUT" "$version"
  python3 eng/release/artifact-record.py verify-registry "$OUT" "$version"
else
  if [[ -f "$OUT/release-$version.json" ]] && python3 -c 'import json,sys; sys.exit(0 if json.load(open(sys.argv[1])).get("publication_started") else 1)' "$OUT/release-$version.json"; then
    die "publication already started for these preserved artifacts; use --resume"
  fi
  say "build and pack $slnx at $version"
  dotnet restore "$slnx"
  dotnet build "$slnx" -c Release --no-restore -p:Version="$version"
  mkdir -p "$OUT"
  find "$OUT" -maxdepth 1 \( -name '*.nupkg' -o -name '*.tgz' \) -delete
  dotnet pack "$slnx" -c Release --no-build -o "$OUT" -p:Version="$version"
  say "pack TypeScript and verify exactly four named artifacts"
  npm pack ./typescript --pack-destination "$OUT" >/dev/null
  python3 eng/release/verify-artifact-set.py "$OUT" "$version"
  rm -f "$OUT/release-$version.json"
  python3 eng/release/artifact-record.py create "$OUT" "$version" "$release_commit"
fi
./eng/release/verify-packages.sh "$version" "$OUT" "$OUT"
./eng/release/verify-typescript.sh "$version" "$OUT/ancplua-qyl-semantic-conventions-$version.tgz"

say "NuGet and npm publication"
[[ $(git rev-parse HEAD) == "$workflow_commit" && -z $(git status --porcelain --untracked-files=no) ]] || die "tracked source changed while packing; commit, re-run the gate and retry"
if (( dry )); then
  for id in "${PACKAGES[@]}"; do
    published=$(latest "$id") && [[ -n $published ]] || die "cannot read the published versions of $id from NuGet"
    # A rejected key fails every attempt; a dropped connection does not.
    probed=0
    for _ in 1 2 3; do key_probe "$id" "$published" && { probed=1; break; }; sleep 3; done
    (( probed )) || die "the NuGet key failed the probe for $id three times: expired, revoked, without push scope, or its account does not own $id (vitals keys)"
    echo "key probe: $id ok (verified against $published, nothing published)"
  done
  NPM_CONFIG_TOKEN=$(<"$NPM_TOKEN_FILE") publish_npm bun publish --cwd typescript "$OUT/ancplua-qyl-semantic-conventions-$version.tgz" --registry "$NPM_REGISTRY" --access public --dry-run </dev/null
  say "dry run finished: nothing published, nothing tagged"
  git status --short --untracked-files=no
  echo "The real run then starts the detached verification ($verify_log, ./release.sh --wait $version):"
  echo "  $VERIFY_COMMAND --published-version $version ($VERIFY_WHAT),"
  echo "  then $QYL_AT_DIR/scripts/refresh-published-versions.sh; the log ends with VERIFIED or FAILED."
  exit 0
fi
if (( ! resume )); then
  python3 eng/release/artifact-record.py mark-started "$OUT" "$version"
  for id in "${PACKAGES[@]}"; do
    # --skip-duplicate: a rerun after a partial push (pushed, not indexed yet) goes on.
    dotnet nuget push "$OUT/$id.$version.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE" --skip-duplicate
  done
fi

# Publish the verified tarball itself; stop immediately on an interactive auth request.
NPM_CONFIG_TOKEN=$(<"$NPM_TOKEN_FILE") publish_npm bun publish --cwd typescript "$OUT/ancplua-qyl-semantic-conventions-$version.tgz" --registry "$NPM_REGISTRY" --access public </dev/null || die "npm publication failed; if a token or OTP is requested, renew credentials before continuing"

say "tag and GitHub release (notes from CHANGELOG.md)"
git show "$release_commit:CHANGELOG.md" | awk -v v="$version" 'index($0, "## [" v "]") == 1 {on=1; next} on && /^## \[/ {exit} on' >"$notes"
git tag -a "v$version" "$release_commit" -m "v$version"
git push -q origin "v$version"
if gh release view "v$version" >/dev/null 2>&1; then
  gh release upload "v$version" "$OUT"/Qyl.Telemetry.SemanticConventions*."$version".nupkg "$OUT/ancplua-qyl-semantic-conventions-$version.tgz" --clobber
else
  gh release create "v$version" --title "v$version" --notes-file "$notes" "$OUT"/Qyl.Telemetry.SemanticConventions*."$version".nupkg "$OUT/ancplua-qyl-semantic-conventions-$version.tgz"
fi

start_verification
say "next (SKILL.md): ./release.sh --wait $version; once VERIFIED, notify AutoInstrumentation (Directory.Packages.props, the single source; the registry ref derives from it), qyl (Version.props QylSemanticConventionsVersion), qyl.mcp (server/ and workbench/ package.json, bun.lock) and qyl.at (src/data/site.ts)"
