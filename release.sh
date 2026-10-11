#!/usr/bin/env bash
# Publish and release the three Qyl.Telemetry.SemanticConventions NuGet packages from this
# machine with the key registered in Vitals. No workflow publishes.
#
#   ./release.sh <x.y.z>            build, pack exactly three packages, push, tag v<x.y.z>, GitHub release
#   ./release.sh <x.y.z> --dry-run  every check, the real build and pack, a NuGet key probe; nothing changes
#
# Version sources that must already agree on HEAD: Directory.Build.props VersionPrefix,
# registry/manifest.yaml schema_url (https://qyl.at/schemas/<x.y.z>) and a CHANGELOG.md
# "## [x.y.z]" entry, with scripts/generate.sh run. qyl.at must serve the schema URL before
# the real push (SKILL.md). The TypeScript package (typescript/) is published by its owner's
# step once it exists; this script ships NuGet only.
set -euo pipefail
cd "$(dirname "$0")"

NUGET_SOURCE=https://api.nuget.org/v3/index.json
NUGET_FLAT=https://api.nuget.org/v3-flatcontainer
NUGET_KEY_FILE=$HOME/.config/vitals/nuget-qyl-manual-publish-key  # vitals keys: "qyl-dev manual publish and release (NuGet)"
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
[[ $(git branch --show-current) == main ]] || die "not on main"
[[ -z $(git status --porcelain --untracked-files=no) ]] || die "working tree not clean"
git fetch -q origin main
[[ $(git rev-parse HEAD) == $(git rev-parse origin/main) ]] || die "main differs from origin/main"
[[ -z $(git ls-remote --tags origin "refs/tags/v$version") ]] || die "tag v$version already on origin"
git rev-parse -q --verify "refs/tags/v$version" >/dev/null && die "tag v$version already exists locally"
prefix=$(sed -n 's:.*<VersionPrefix[^>]*>\([^<]*\)</VersionPrefix>.*:\1:p' Directory.Build.props | head -1)
schema=$(sed -n 's#^schema_url: https://qyl.at/schemas/##p' registry/manifest.yaml)
[[ $prefix == "$version" ]] || die "Directory.Build.props VersionPrefix is $prefix, not $version"
[[ $schema == "$version" ]] || die "registry/manifest.yaml schema_url names $schema, not $version"
grep -q "^## \[$version\]" CHANGELOG.md || die "CHANGELOG.md has no '## [$version]' entry"
for id in "${PACKAGES[@]}"; do
  if indexed "$id" "$version"; then (( dry )) || die "$id $version is already on NuGet"; echo "warning: $id $version is already on NuGet; the real run refuses"; fi
done
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
find "$OUT" -maxdepth 1 -name '*.nupkg' -delete 2>/dev/null || true
dotnet pack "$slnx" -c Release --no-build -o "$OUT" -p:Version="$version"

say "exactly three packages (SC-9)"
expected=$(printf '%s\n' "${PACKAGES[@]/%/.$version.nupkg}" | sort)
actual=$(find "$OUT" -maxdepth 1 -type f -name '*.nupkg' -exec basename {} \; | sort)
[[ $actual == "$expected" ]] || { echo "expected:"; echo "$expected"; echo "packed:"; echo "${actual:-<none>}"; die "package set mismatch"; }
echo "$actual"

say "NuGet push"
probe_dir=$(mktemp -d); notes=$(mktemp); trap 'rm -rf "$probe_dir" "$notes"' EXIT
if (( dry )); then
  id=$(lower "${PACKAGES[0]}")
  latest=$(curl -fsSL "$NUGET_FLAT/$id/index.json" | python3 -I -c 'import json,sys; print(json.load(sys.stdin)["versions"][-1])')
  curl -fsSL -o "$probe_dir/probe.nupkg" "$NUGET_FLAT/$id/$latest/$id.$latest.nupkg"
  echo "dry run: pushing the published ${PACKAGES[0]} $latest again with --skip-duplicate to prove the key (expect 'already exists')"
  dotnet nuget push "$probe_dir/probe.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE" --skip-duplicate
  say "dry run finished: nothing published, nothing tagged"
  git status --short --untracked-files=no
  exit 0
fi
for id in "${PACKAGES[@]}"; do
  dotnet nuget push "$OUT/$id.$version.nupkg" --api-key "$(<"$NUGET_KEY_FILE")" --source "$NUGET_SOURCE"
done

say "verify the published packages"
./eng/release/verify-packages.sh "$version" "$NUGET_SOURCE" "$OUT"

say "tag and GitHub release (notes from CHANGELOG.md)"
awk -v v="$version" 'index($0, "## [" v "]") == 1 {on=1; next} on && /^## \[/ {exit} on' CHANGELOG.md >"$notes"
git tag -a "v$version" -m "v$version"
git push -q origin "v$version"
gh release create "v$version" --title "v$version" --notes-file "$notes" "$OUT"/Qyl.Telemetry.SemanticConventions*."$version".nupkg

say "next (SKILL.md): once indexed, notify AutoInstrumentation (Directory.Packages.props, SEMCONV_REF), qyl (Version.props QylSemanticConventionsVersion) and qyl.at (src/data/site.ts)"
