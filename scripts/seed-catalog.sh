#!/usr/bin/env bash
# Pull the resolved semantic-conventions registry from upstream and overwrite
# master-programmatic.yaml with the contents. Argument: semconv version (default:
# value of SemConvSchemaVersion in Version.props).
#
# This is intentionally NOT auto-invoked by the build — registry bumps are a
# human-reviewed change because they ripple into the generated attribute .g.cs
# and require a corresponding `./build.sh SeedAttributesHash` to refresh the
# lock file consumed by VerifyAttributesHash.
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

DEFAULT_VERSION="$(/usr/bin/sed -nE 's|.*<SemConvSchemaVersion>(.*)</SemConvSchemaVersion>.*|\1|p' "$REPO_ROOT/Version.props")"
VERSION="${1:-$DEFAULT_VERSION}"

if [[ -z "$VERSION" ]]; then
  echo "seed-catalog.sh: could not determine semconv version (no arg, no <SemConvSchemaVersion>)" >&2
  exit 64
fi

OUT="$REPO_ROOT/master-programmatic.yaml"
URL="https://raw.githubusercontent.com/open-telemetry/semantic-conventions/v$VERSION/internal/tools/schemas/resolved-schema-$VERSION.yaml"

echo "seed-catalog.sh: pulling resolved registry for v$VERSION"
echo "  source: $URL"
echo "  dest:   $OUT"

# Fallback chain: the schemas/ layout shifts across releases, so probe two paths.
if ! curl --fail --silent --show-error --location "$URL" -o "$OUT.tmp"; then
  ALT_URL="https://raw.githubusercontent.com/open-telemetry/semantic-conventions/v$VERSION/schemas/$VERSION"
  echo "seed-catalog.sh: primary URL failed, trying $ALT_URL"
  curl --fail --silent --show-error --location "$ALT_URL" -o "$OUT.tmp"
fi

mv "$OUT.tmp" "$OUT"
SHA="$(shasum -a 256 "$OUT" | awk '{print $1}')"
SIZE="$(wc -c <"$OUT" | tr -d ' ')"

echo "seed-catalog.sh: wrote $SIZE bytes; sha256=$SHA"
echo
echo "Next steps:"
echo "  1. Regenerate Attributes/**/*.g.cs from this registry (use your local Weaver pipeline)."
echo "  2. ./build.sh SeedAttributesHash    # refresh the lock file"
echo "  3. git diff src/                    # review what changed before committing"
