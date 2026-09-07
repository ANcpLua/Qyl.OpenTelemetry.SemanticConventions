#!/usr/bin/env bash
# Materialises the filtered core registry the qyl manifest depends on.
#
# registry/manifest.yaml is the only place the core pin lives: the tag is read from the
# `core` dependency's schema_url (https://opentelemetry.io/schemas/<version> -> v<version>).
# The archive is filtered exactly the way upstream semantic-conventions-genai filters it in
# its own Makefile — gen-ai/, mcp/, openai/ and the `registry.aws.bedrock` group removed —
# because a direct *unfiltered* core dependency collides with the genai dependency:
#   Ambiguous reference 'gen_ai.agent.description' ... gen-ai-dev/1.42.0-dev and .../1.44.0
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
manifest="${repo_root}/registry/manifest.yaml"
work_dir="${repo_root}/.build"
out_dir="${work_dir}/core-filtered"

CORE_REMOTE="${SEMCONV_CORE_REMOTE:-https://github.com/open-telemetry/semantic-conventions.git}"
CORE_REPO="${SEMCONV_CORE_REPO:-${work_dir}/semantic-conventions}"

core_ref="$(python3 - "${manifest}" <<'PY'
import sys
import yaml

manifest = yaml.safe_load(open(sys.argv[1])) or {}
for dependency in manifest.get("dependencies") or []:
    if dependency.get("name") != "core":
        continue
    schema_url = dependency.get("schema_url", "")
    prefix = "https://opentelemetry.io/schemas/"
    if not schema_url.startswith(prefix):
        raise SystemExit(f"error: core schema_url has unexpected shape: {schema_url!r}")
    print("v" + schema_url[len(prefix):])
    break
else:
    raise SystemExit("error: registry/manifest.yaml has no 'core' dependency")
PY
)"

if [[ ! -d "${CORE_REPO}/.git" ]]; then
  mkdir -p "$(dirname "${CORE_REPO}")"
  git clone --filter=blob:none "${CORE_REMOTE}" "${CORE_REPO}"
fi
git -C "${CORE_REPO}" fetch --prune origin '+refs/heads/*:refs/remotes/origin/*' '+refs/tags/*:refs/tags/*'

core_commit="$(git -C "${CORE_REPO}" rev-parse "${core_ref}^{commit}")"

rm -rf "${out_dir}"
mkdir -p "${out_dir}"
git -C "${CORE_REPO}" archive "${core_commit}" model | tar -x -C "${out_dir}"

for required in http manifest.yaml; do
  if [[ ! -e "${out_dir}/model/${required}" ]]; then
    echo "error: core semantic-conventions model is missing ${required}" >&2
    exit 1
  fi
done

rm -rf "${out_dir}/model/gen-ai" "${out_dir}/model/mcp" "${out_dir}/model/openai"

aws_registry="${out_dir}/model/aws/registry.yaml"
if [[ -f "${aws_registry}" ]]; then
  awk -v gid="registry.aws.bedrock" '
    BEGIN { skip = 0 }
    /^  - id: / { skip = ($0 == "  - id: " gid) }
    !skip { print }
  ' "${aws_registry}" > "${aws_registry}.tmp"
  mv "${aws_registry}.tmp" "${aws_registry}"
fi

printf '%s\n' "${core_commit}" > "${work_dir}/core-commit.txt"
printf '%s\n' "${core_ref}" > "${work_dir}/core-ref.txt"

echo "core ${core_ref} (${core_commit}) -> ${out_dir}/model"
