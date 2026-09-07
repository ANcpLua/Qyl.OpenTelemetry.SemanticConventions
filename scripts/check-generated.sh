#!/usr/bin/env bash
# CI's drift gate: regenerate everything from registry/ and fail if the working tree moved.
# Same shape as the `check-generated` target in opentelemetry-weaver-examples/basic/Makefile.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
cd "${repo_root}"

"${script_dir}/generate.sh"

generated_paths=(
  "src/Qyl.Telemetry.SemanticConventions/Generated"
  "src/Qyl.Telemetry.SemanticConventions.Incubating/Generated"
  "src/Qyl.Telemetry.SemanticConventions.Analyzers/SemconvRegistryFacts.g.cs"
  "src/Qyl.Telemetry.SemanticConventions.Analyzers/SemconvDeprecations.g.cs"
  "generated"
)

if ! git diff --exit-code -- "${generated_paths[@]}"; then
  echo "FAIL: generated code is out of sync with registry/." >&2
  echo "Run scripts/generate.sh and commit the result." >&2
  exit 1
fi

untracked="$(git ls-files --others --exclude-standard -- "${generated_paths[@]}")"
if [[ -n "${untracked}" ]]; then
  echo "FAIL: generation produced files that are not committed:" >&2
  echo "${untracked}" >&2
  exit 1
fi

echo "OK: generated code is up to date"
