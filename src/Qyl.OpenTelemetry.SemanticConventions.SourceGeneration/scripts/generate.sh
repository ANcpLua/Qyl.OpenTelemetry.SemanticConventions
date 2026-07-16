#!/usr/bin/env bash
# Regenerate Resources/resolved-registry.json from two source registries:
#   1. core open-telemetry/semantic-conventions at the version in Version.props
#   2. open-telemetry/semantic-conventions-genai at a pinned commit
#
# The generated projection is qyl-owned JSON consumed by the Roslyn source
# generator. It is not Weaver's resolved-registry-v2 contract.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "${script_dir}/.." && pwd)"
repo_root="$(cd "${project_dir}/../.." && pwd)"
templates_dir="${script_dir}/templates"
output_file="${project_dir}/Resources/resolved-registry.json"
work_dir="${repo_root}/.build/semconv-source-generation"

version_props="${repo_root}/Version.props"

read_version_property() {
  python3 - "${version_props}" "$1" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
property_name = sys.argv[2]
value = root.findtext(f".//{property_name}")
if value is None or not value.strip():
    raise SystemExit(f"error: Version.props does not define {property_name}")
print(value.strip())
PY
}

default_schema_version="$(read_version_property SemConvSchemaVersion)"
default_weaver_version="$(read_version_property WeaverVersion)"

SCHEMA_VERSION="${SEMCONV_SCHEMA_VERSION:-${default_schema_version}}"
EXPECTED_WEAVER_VERSION="${SEMCONV_WEAVER_VERSION:-${default_weaver_version}}"
CORE_REF="${SEMCONV_CORE_REF:-v${SCHEMA_VERSION}}"
CORE_REPO="${SEMCONV_CORE_REPO:-${repo_root}/.tools/semantic-conventions}"
CORE_REMOTE="${SEMCONV_CORE_REMOTE:-https://github.com/open-telemetry/semantic-conventions.git}"
GENAI_REF="${SEMCONV_GENAI_REF:-c321d7eb4443ae1d1d88c2e24eda849f62049008}"
GENAI_REPO="${SEMCONV_GENAI_REPO:-${repo_root}/.tools/semantic-conventions-genai}"
GENAI_REMOTE="${SEMCONV_GENAI_REMOTE:-https://github.com/open-telemetry/semantic-conventions-genai.git}"

# Set WEAVER to a command if the published container is unavailable, for example:
#   WEAVER="/Users/.../weaver/target/release/weaver" ./scripts/generate.sh
#   WEAVER="cargo run --quiet --manifest-path /Users/.../weaver/Cargo.toml --bin weaver --" ./scripts/generate.sh
WEAVER="${WEAVER:-weaver}"
read -r -a WEAVER_CMD <<< "${WEAVER}"

ensure_upstream_repo() {
  local repo="$1"
  local remote="$2"
  local label="$3"
  if [[ ! -d "${repo}/.git" ]]; then
    mkdir -p "$(dirname "${repo}")"
    git clone "${remote}" "${repo}"
  fi

  local actual_remote
  actual_remote="$(git -C "${repo}" remote get-url origin 2>/dev/null || true)"
  if [[ -z "${actual_remote}" ]]; then
    echo "error: ${label} repo at ${repo} has no origin remote" >&2
    exit 1
  fi

  git -C "${repo}" fetch --prune origin '+refs/heads/*:refs/remotes/origin/*' '+refs/tags/*:refs/tags/*'
}

require_path() {
  local path="$1"
  local message="$2"
  if [[ ! -e "${path}" ]]; then
    echo "error: ${message}: ${path}" >&2
    exit 1
  fi
}

validate_core_source() {
  local model_dir="$1"
  require_path "${model_dir}/http" "core semantic-conventions model is missing http/"
  require_path "${model_dir}/manifest.yaml" "core semantic-conventions model is missing manifest.yaml"
}

validate_genai_source() {
  local model_dir="$1"
  require_path "${model_dir}/manifest.yaml" "semantic-conventions-genai model is missing manifest.yaml"
  require_path "${model_dir}/gen-ai" "semantic-conventions-genai model is missing gen-ai/"
  require_path "${model_dir}/mcp" "semantic-conventions-genai model is missing mcp/"
  require_path "${model_dir}/openai" "semantic-conventions-genai model is missing openai/"
  require_path "${model_dir}/aws-bedrock" "semantic-conventions-genai model is missing aws-bedrock/"
}

commit_for_ref() {
  local repo="$1"
  local ref="$2"
  git -C "${repo}" rev-parse "${ref}^{commit}"
}

date_epoch_for_commit() {
  local repo="$1"
  local commit="$2"
  git -C "${repo}" show -s --format=%ct "${commit}"
}

archive_ref() {
  local repo="$1"
  local ref="$2"
  local destination="$3"
  mkdir -p "${destination}"
  git -C "${repo}" archive "${ref}" | tar -x -C "${destination}"
}

strip_migrated_core_scopes() {
  local model_dir="$1"
  rm -rf \
    "${model_dir}/gen-ai" \
    "${model_dir}/mcp" \
    "${model_dir}/openai"

  local aws_registry="${model_dir}/aws/registry.yaml"
  if [[ -f "${aws_registry}" ]]; then
    awk -v gid="registry.aws.bedrock" '
      BEGIN { skip = 0 }
      /^  - id: / { skip = ($0 == "  - id: " gid) }
      !skip { print }
    ' "${aws_registry}" > "${aws_registry}.tmp"
    mv "${aws_registry}.tmp" "${aws_registry}"
  fi
}

weaver_version() {
  "${WEAVER_CMD[@]}" --version | awk '{print $2}'
}

run_weaver_projection() {
  local cwd="$1"
  local registry_path="$2"
  local output_dir="$3"
  local source_registry="$4"
  local source_ref="$5"
  local source_commit="$6"
  local source_date_epoch="$7"
  local source_schema_version="$8"
  local actual_weaver_version="$9"

  mkdir -p "${output_dir}"
  (
    cd "${cwd}"
    "${WEAVER_CMD[@]}" registry generate \
      -r "${registry_path}" \
      --v2 \
      --skip-policies \
      -t "${templates_dir}" \
      -D "schema_version=${source_schema_version}" \
      -D "source_registry=${source_registry}" \
      -D "source_ref=${source_ref}" \
      -D "source_commit=${source_commit}" \
      -D "source_date_epoch=${source_date_epoch}" \
      -D "weaver_version=${actual_weaver_version}" \
      ./ \
      "${output_dir}/./"
  )
}

merge_projected_registries() {
  local core_json="$1"
  local genai_json="$2"
  local core_model_dir="$3"
  local genai_model_dir="$4"
  local destination="$5"
  local schema_version="$6"
  local actual_weaver_version="$7"

  python3 - "$core_json" "$genai_json" "$core_model_dir" "$genai_model_dir" "$destination" "$schema_version" "$actual_weaver_version" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

try:
    import yaml
except ImportError as exc:
    raise SystemExit("error: PyYAML is required to rehydrate event body metadata from source YAML") from exc

core_path, genai_path, core_model_dir, genai_model_dir, destination, schema_version, weaver_version = sys.argv[1:]
core = json.loads(Path(core_path).read_text())
genai = json.loads(Path(genai_path).read_text())

def unique_by(rows, key):
    result = {}
    order = []
    for row in rows:
        row_key = key(row)
        if row_key not in result:
            order.append(row_key)
        result[row_key] = row
    return [result[row_key] for row_key in order]

def group_key(row):
    return (row.get("type", ""), row.get("id", ""))

def catalog_key(row):
    return row.get("key", "")

def metric_key(row):
    return row.get("metric_name", "")

def event_key(row):
    return row.get("event_name", "")

def collect_event_bodies(*model_dirs):
    bodies = {}
    for model_dir in model_dirs:
        root = Path(model_dir)
        for path in root.rglob("*.yaml"):
            data = yaml.safe_load(path.read_text()) or {}
            for group in data.get("groups", []):
                if group.get("type") == "event" and "body" in group:
                    name = group.get("name")
                    if name:
                        bodies[name] = group["body"]
    return bodies

def rehydrate_event_bodies(events, bodies):
    for event in events:
        name = event.get("event_name")
        if name in bodies and "body" not in event:
            event["body"] = bodies[name]
    return events

def source_metadata(source_registry, source_by_registry):
    source = source_by_registry[source_registry]
    return {
        "source_registry": source["source_registry"],
        "schema_url": source["schema_url"],
        "source_ref": source["source_ref"],
        "source_commit": source["source_commit"],
        "source_date_epoch": source["source_date_epoch"],
    }

def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def model_path(path, model_dir):
    return "model/" + path.relative_to(model_dir).as_posix()

def classify_model_file(path):
    if path.name == "manifest.yaml":
        return "manifest"
    if path.suffix == ".json":
        return "json_schema"
    if path.suffix in {".yaml", ".yml"}:
        return "registry_definition"
    return "supporting"

def collect_model_files(model_dirs, source_by_registry):
    files = []
    for source_registry, model_dir_value in sorted(model_dirs.items()):
        model_dir = Path(model_dir_value)
        for path in sorted(candidate for candidate in model_dir.rglob("*") if candidate.is_file()):
            files.append({
                "path": model_path(path, model_dir),
                "kind": classify_model_file(path),
                "sha256": sha256(path),
                **source_metadata(source_registry, source_by_registry),
            })
    return files

def collect_manifests(model_dirs, source_by_registry):
    manifests = []
    for source_registry, model_dir_value in sorted(model_dirs.items()):
        path = Path(model_dir_value) / "manifest.yaml"
        if not path.is_file():
            raise SystemExit(f"error: {source_registry} model is missing manifest.yaml: {path}")
        manifests.append({
            "path": "model/manifest.yaml",
            "document": yaml.safe_load(path.read_text()) or {},
            **source_metadata(source_registry, source_by_registry),
        })
    return manifests

def schema_annotation(attribute):
    annotations = attribute.get("annotations") or {}
    type_annotation = annotations.get("type") or {}
    return type_annotation.get("json_schema")

def collect_json_schemas(catalog, model_dirs, source_by_registry):
    referenced_attributes = {}
    for attribute in catalog:
        schema_path = schema_annotation(attribute)
        if attribute.get("source_registry") == "genai" and attribute.get("type") == "any" and not schema_path:
            raise SystemExit(
                f"error: type:any attribute {attribute.get('key', '<unknown>')} has no annotations.type.json_schema")
        if not schema_path:
            continue
        key = (attribute["source_registry"], schema_path)
        referenced_attributes.setdefault(key, []).append(attribute["key"])

    schemas = []
    for source_registry, model_dir_value in sorted(model_dirs.items()):
        model_dir = Path(model_dir_value)
        discovered = {
            model_path(path, model_dir): path
            for path in model_dir.rglob("*.json")
            if path.is_file()
        }
        referenced_paths = {
            path
            for registry, path in referenced_attributes
            if registry == source_registry
        }

        missing = sorted(referenced_paths - discovered.keys())
        if missing:
            raise SystemExit(
                f"error: {source_registry} registry references missing JSON schemas: {', '.join(missing)}")

        for schema_path, path in sorted(discovered.items()):
            content = path.read_text()
            document = json.loads(content)
            schemas.append({
                "path": schema_path,
                "title": document.get("title", ""),
                "sha256": sha256(path),
                "attribute_keys": sorted(referenced_attributes.get((source_registry, schema_path), [])),
                "content": content,
                "document": document,
                **source_metadata(source_registry, source_by_registry),
            })
    return schemas

sources = unique_by(
    list(core.get("sources", [])) + list(genai.get("sources", [])),
    lambda row: (row.get("source_registry", ""), row.get("source_commit", "")),
)
source_by_registry = {source["source_registry"]: source for source in sources}
model_dirs = {
    "core": core_model_dir,
    "genai": genai_model_dir,
}

events = unique_by(list(core.get("events", [])) + list(genai.get("events", [])), event_key)
events = rehydrate_event_bodies(events, collect_event_bodies(core_model_dir, genai_model_dir))
catalog = unique_by(list(core.get("catalog", [])) + list(genai.get("catalog", [])), catalog_key)

merged = {
    "schema_version": schema_version,
    "schema_url": core.get("schema_url", ""),
    "genai_schema_url": genai.get("schema_url", ""),
    "semconv_commit": core.get("semconv_commit", ""),
    "weaver_version": weaver_version,
    "sources": sources,
    "manifests": collect_manifests(model_dirs, source_by_registry),
    "model_files": collect_model_files(model_dirs, source_by_registry),
    "json_schemas": collect_json_schemas(catalog, model_dirs, source_by_registry),
    "groups": unique_by(list(core.get("groups", [])) + list(genai.get("groups", [])), group_key),
    "catalog": catalog,
    "metrics": unique_by(list(core.get("metrics", [])) + list(genai.get("metrics", [])), metric_key),
    "events": events,
}

Path(destination).parent.mkdir(parents=True, exist_ok=True)
Path(destination).write_text(json.dumps(merged, indent=2, ensure_ascii=False) + "\n")
PY
}

actual_weaver_version="$(weaver_version)"
if [[ "${actual_weaver_version}" != "${EXPECTED_WEAVER_VERSION}" ]]; then
  echo "error: Weaver ${EXPECTED_WEAVER_VERSION} is required; found ${actual_weaver_version}" >&2
  echo "set WEAVER to the pinned binary or intentionally update WeaverVersion in Version.props" >&2
  exit 1
fi

ensure_upstream_repo "${CORE_REPO}" "${CORE_REMOTE}" "core semantic-conventions"
ensure_upstream_repo "${GENAI_REPO}" "${GENAI_REMOTE}" "semantic-conventions-genai"

core_commit="$(commit_for_ref "${CORE_REPO}" "${CORE_REF}")"
genai_commit="$(commit_for_ref "${GENAI_REPO}" "${GENAI_REF}")"
core_date_epoch="$(date_epoch_for_commit "${CORE_REPO}" "${core_commit}")"
genai_date_epoch="$(date_epoch_for_commit "${GENAI_REPO}" "${genai_commit}")"

rm -rf "${work_dir}"
mkdir -p "${work_dir}"

archive_ref "${CORE_REPO}" "${core_commit}" "${work_dir}/core-source"
validate_core_source "${work_dir}/core-source/model"
mkdir -p "${work_dir}/core-filtered"
cp -R "${work_dir}/core-source/model" "${work_dir}/core-filtered/model"
strip_migrated_core_scopes "${work_dir}/core-filtered/model"

archive_ref "${GENAI_REPO}" "${genai_commit}" "${work_dir}/genai-source"
validate_genai_source "${work_dir}/genai-source/model"
mkdir -p "${work_dir}/genai-source/.build"
cp -R "${work_dir}/core-filtered/model" "${work_dir}/genai-source/.build/sc-upstream-filtered"

core_projection_dir="${work_dir}/core-projection"
genai_projection_dir="${work_dir}/genai-projection"

run_weaver_projection \
  "${repo_root}" \
  "${work_dir}/core-filtered/model" \
  "${core_projection_dir}" \
  "core" \
  "${CORE_REF}" \
  "${core_commit}" \
  "${core_date_epoch}" \
  "${SCHEMA_VERSION}" \
  "${actual_weaver_version}"

run_weaver_projection \
  "${work_dir}/genai-source" \
  "./model" \
  "${genai_projection_dir}" \
  "genai" \
  "${GENAI_REF}" \
  "${genai_commit}" \
  "${genai_date_epoch}" \
  "gen-ai-dev/1.42.0-dev" \
  "${actual_weaver_version}"

merge_projected_registries \
  "${core_projection_dir}/Resources/resolved-registry.json" \
  "${genai_projection_dir}/Resources/resolved-registry.json" \
  "${work_dir}/core-filtered/model" \
  "${work_dir}/genai-source/model" \
  "${output_file}" \
  "${SCHEMA_VERSION}" \
  "${actual_weaver_version}"

python3 "${script_dir}/emit_analyzer_registry.py" --registry "${output_file}"
python3 "${script_dir}/emit_registry_resources.py" --registry "${output_file}"

echo "Regenerated ${output_file}"
echo "Regenerated registry-derived analyzer facts and public payload-schema resources"
echo "  core:  ${CORE_REF} (${core_commit})"
echo "  genai: ${GENAI_REF} (${genai_commit})"
echo "  weaver: ${actual_weaver_version}"
