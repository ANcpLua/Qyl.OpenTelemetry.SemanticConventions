#!/usr/bin/env bash
# Publish the three NuGet packages and the shared TypeScript package from this
# machine with the keys registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            build, pack exactly four artifacts, publish, tag v<x.y.z>, GitHub release
#   ./release.sh <x.y.z> --dry-run  every check, the real build and pack, NuGet/npm authentication probes; nothing published
#
# Version sources that must already agree on HEAD: Directory.Build.props VersionPrefix,
# registry/manifest.yaml schema_url (https://qyl.at/schemas/<x.y.z>) and a CHANGELOG.md
# "## [x.y.z]" entry, with scripts/generate.sh run. qyl.at must serve the schema URL before
# the real push (SKILL.md). Both registries receive the same version.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
NPM_REGISTRY=https://registry.npmjs.org
NPM_PACKAGE=@ancplua/qyl-semantic-conventions
NPM_TOKEN_FILE=$HOME/.config/vitals/npm-qyl-manual-publish-token
OUT=artifacts
PACKAGES=(Qyl.Telemetry.SemanticConventions Qyl.Telemetry.SemanticConventions.Analyzers Qyl.Telemetry.SemanticConventions.Incubating)

version=${1:-}
dry=0; [[ ${2:-} == --dry-run ]] && dry=1
[[ $version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: $0 <x.y.z> [--dry-run]" >&2; exit 2; }
say() { printf '\n== %s\n' "$*"; }
die() { echo "release: $*" >&2; exit 1; }
lower() { tr '[:upper:]' '[:lower:]' <<<"$1"; }
indexed() { curl -fsSL "$NUGET_FLAT/$(lower "$1")/index.json" 2>/dev/null | grep -q "\"$2\""; }

say "checks"
[[ -r $NUGET_KEY_FILE ]] || die "NuGet key missing: $NUGET_KEY_FILE (vitals keys)"
[[ -r $NPM_TOKEN_FILE ]] || die "npm token missing: $NPM_TOKEN_FILE (vitals keys)"
probe_dir=$(mktemp -d); notes=$(mktemp); npmrc=$(mktemp)
chmod 600 "$npmrc"
trap 'rm -rf "$probe_dir" "$notes" "$npmrc"' EXIT
printf '//registry.npmjs.org/:_authToken=%s\n' "$(<"$NPM_TOKEN_FILE")" >"$npmrc"
NPM_CONFIG_USERCONFIG=$npmrc npm whoami --registry="$NPM_REGISTRY" >/dev/null || die "npm token rejected (vitals keys); renew it before publication"
[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "working tree not clean"
git fetch -q origin main
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main differs from origin/main"
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
  if indexed "$id" "$version"; then (( dry )) || die "$id $version is already on NuGet"; echo "warning: $id $version is already on NuGet; the real run refuses"; fi
done
if npm view "$NPM_PACKAGE@$version" version --registry="$NPM_REGISTRY" >/dev/null 2>&1; then
  (( dry )) || die "$NPM_PACKAGE $version is already on npm"
  echo "warning: $NPM_PACKAGE $version is already on npm; the real run refuses"
fi
if curl -fsS --retry 2 -o /dev/null "https://qyl.at/schemas/$version"; then
  echo "qyl.at serves https://qyl.at/schemas/$version"
else
  (( dry )) || die "https://qyl.at/schemas/$version is not served; add public/schemas/$version in qyl.at and deploy it first"
  echo "warning: https://qyl.at/schemas/$version is not served yet; the real run refuses until qyl.at deploys it"
fi
gh auth status >/dev/null 2>&1 || die "gh is not logged in"
slnx=$(find . -maxdepth 1 -type f \( -name '*.slnx' -o -name '*.sln' \) -print -quit | sed 's#^\./##')
[[ -n $slnx ]] || die "no .slnx or .sln at the repo root"

say "build and pack $slnx at $version"
dotnet restore "$slnx"
dotnet build "$slnx" -c Release --no-restore -p:Version="$version"
mkdir -p "$OUT"
find "$OUT" -maxdepth 1 \( -name '*.nupkg' -o -name '*.tgz' \) -delete
dotnet pack "$slnx" -c Release --no-build -o "$OUT" -p:Version="$version"

say "pack TypeScript and verify exactly four named artifacts"
npm pack ./typescript --pack-destination "$OUT" >/dev/null
python3 eng/release/verify-artifact-set.py "$OUT" "$version"
./eng/release/verify-typescript.sh "$version" "$PWD/$OUT/ancplua-qyl-semantic-conventions-$version.tgz"

say "NuGet and npm publication"
if (( dry )); then
  id=$(lower "${PACKAGES[0]}")
  latest=$(curl -fsSL "$NUGET_FLAT/$id/index.json" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["versions"][-1])')
  curl -fsSL -o "$probe_dir/probe.nupkg" "$NUGET_FLAT/$id/$latest/$id.$latest.nupkg"
  echo "dry run: pushing the published ${PACKAGES[0]} $latest again with --skip-duplicate to prove the key (expect 'already exists')"
  dotnet nuget push "$probe_dir/probe.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE" --skip-duplicate
  NPM_CONFIG_TOKEN=$(<"$NPM_TOKEN_FILE") bun publish "$OUT/ancplua-qyl-semantic-conventions-$version.tgz" --registry "$NPM_REGISTRY" --access public --dry-run </dev/null
  say "dry run finished: nothing published, nothing tagged"
  git status --short --untracked-files=no
  exit 0
fi
for id in "${PACKAGES[@]}"; do
  dotnet nuget push "$OUT/$id.$version.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE"
done

# Publish the verified tarball itself; stdin is closed so an OTP request cannot hang.
NPM_CONFIG_TOKEN=$(<"$NPM_TOKEN_FILE") bun publish "$OUT/ancplua-qyl-semantic-conventions-$version.tgz" --registry "$NPM_REGISTRY" --access public </dev/null || die "npm publication failed; if a token or OTP is requested, renew credentials before continuing"

say "wait until every package is indexed"
for id in "${PACKAGES[@]}"; do
  for attempt in $(seq 1 120); do
    indexed "$id" "$version" && break
    echo "Waiting for $id $version ($attempt/120)"
    sleep 10
  done
  indexed "$id" "$version" || die "$id $version did not index"
done
for attempt in $(seq 1 120); do
  [[ $(npm view "$NPM_PACKAGE@$version" version --registry="$NPM_REGISTRY" 2>/dev/null || true) == "$version" ]] && break
  echo "Waiting for $NPM_PACKAGE $version ($attempt/120)"
  sleep 10
done
[[ $(npm view "$NPM_PACKAGE@$version" version --registry="$NPM_REGISTRY") == "$version" ]] || die "npm package did not index"
printf 'INDEXED %s %s\n' "$version" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"

say "verify the published packages"
./eng/release/verify-packages.sh "$version" "$NUGET_SOURCE" "$OUT"
./eng/release/verify-typescript.sh "$version"

say "tag and GitHub release (notes from CHANGELOG.md)"
awk -v v="$version" 'index($0, "## [" v "]") == 1 {on=1; next} on && /^## \[/ {exit} on' CHANGELOG.md >"$notes"
git tag -a "v$version" -m "v$version"
git push -q origin "v$version"
gh release create "v$version" --title "v$version" --notes-file "$notes" "$OUT"/Qyl.Telemetry.SemanticConventions*."$version".nupkg "$OUT/ancplua-qyl-semantic-conventions-$version.tgz"

say "next (SKILL.md): once indexed, notify AutoInstrumentation (Directory.Packages.props, SEMCONV_REF), qyl (Version.props QylSemanticConventionsVersion) , qyl.mcp (server/package.json and bun.lock) and qyl.at (src/data/site.ts)"
