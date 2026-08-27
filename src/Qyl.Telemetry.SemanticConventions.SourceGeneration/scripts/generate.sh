#!/usr/bin/env bash
# Regenerate Resources/resolved-registry.json from three source registries:
#   1. core open-telemetry/semantic-conventions at SemConvSchemaVersion in Version.props
#   2. open-telemetry/semantic-conventions-genai at SemConvGenAiRef in Version.props
#   3. the qyl-owned Resources/qyl-registry.json (attributes -> catalog, metrics ->
#      metrics + groups, scope_names / event_names -> root), rows tagged
#      source_registry "qyl"
#
# Since the upstream GenAI split, core@1.44.0 carries only deprecated/ under
# model/gen-ai/ — the living gen_ai.* model exists exclusively in the genai
# registry. Two guards below keep a pin bump from silently killing gen_ai.*:
# the genai manifest must depend on core@SemConvSchemaVersion, and the merged
# output must still contain live gen_ai.* attributes.
#
# The generated projection is qyl-owned JSON consumed by the Roslyn source
# generator. It is not Weaver's resolved-registry-v2 contract.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd "${script_dir}/.." && pwd)"
repo_root="$(cd "${project_dir}/../.." && pwd)"
templates_dir="${script_dir}/templates"
output_file="${project_dir}/Resources/resolved-registry.json"
qyl_registry_file="${project_dir}/Resources/qyl-registry.json"
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
default_genai_ref="$(read_version_property SemConvGenAiRef)"

SCHEMA_VERSION="${SEMCONV_SCHEMA_VERSION:-${default_schema_version}}"
EXPECTED_WEAVER_VERSION="${SEMCONV_WEAVER_VERSION:-${default_weaver_version}}"
CORE_REF="${SEMCONV_CORE_REF:-v${SCHEMA_VERSION}}"
CORE_REPO="${SEMCONV_CORE_REPO:-${repo_root}/.tools/semantic-conventions}"
CORE_REMOTE="${SEMCONV_CORE_REMOTE:-https://github.com/open-telemetry/semantic-conventions.git}"
GENAI_REF="${SEMCONV_GENAI_REF:-${default_genai_ref}}"
GENAI_REPO="${SEMCONV_GENAI_REPO:-${repo_root}/.tools/semantic-conventions-genai}"
GENAI_REMOTE="${SEMCONV_GENAI_REMOTE:-https://github.com/open-telemetry/semantic-conventions-genai.git}"

# Set WEAVER to a command if the published container is unavailable. Take the binary
# from the upstream release matching WeaverVersion — a locally built checkout is a
# stale-generator hazard, since a working tree can sit at any revision while still
# reporting a version this script accepts. The version guard below compares
# `weaver --version`, which cannot tell a release binary from a build of an
# arbitrary commit that happens to carry the same version string.
#
#   gh release download "v${EXPECTED_WEAVER_VERSION}" --repo open-telemetry/weaver \
#     --pattern 'weaver-aarch64-apple-darwin.tar.xz*'
#   shasum -a 256 -c weaver-aarch64-apple-darwin.tar.xz.sha256
#   tar -xJf weaver-aarch64-apple-darwin.tar.xz
#   WEAVER="$PWD/weaver-aarch64-apple-darwin/weaver" ./scripts/generate.sh
WEAVER="${WEAVER:-weaver}"
read -r -a WEAVER_CMD <<< "${WEAVER}"
# run_weaver_projection runs from per-registry work dirs, so a relative
# weaver path would stop resolving after the first projection.
if [[ "${WEAVER_CMD[0]}" == */* && "${WEAVER_CMD[0]}" != /* && -e "${WEAVER_CMD[0]}" ]]; then
  WEAVER_CMD[0]="$(cd "$(dirname "${WEAVER_CMD[0]}")" && pwd)/$(basename "${WEAVER_CMD[0]}")"
fi

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

genai_manifest_schema_version() {
  local manifest="$1"
  python3 - "${manifest}" <<'PY'
import sys

import yaml

manifest = yaml.safe_load(open(sys.argv[1])) or {}
schema_url = manifest.get("schema_url", "")
prefix = "https://opentelemetry.io/schemas/"
if not schema_url.startswith(prefix):
    raise SystemExit(f"error: genai manifest schema_url has unexpected shape: {schema_url!r}")
print(schema_url[len(prefix):])
PY
}

verify_genai_core_dependency() {
  local manifest="$1"
  local expected_core_version="$2"
  python3 - "${manifest}" "${expected_core_version}" <<'PY'
import sys

import yaml

manifest_path, expected = sys.argv[1:]
manifest = yaml.safe_load(open(manifest_path)) or {}
urls = [dep.get("schema_url", "") for dep in manifest.get("dependencies") or []]
expected_url = f"https://opentelemetry.io/schemas/{expected}"
if expected_url not in urls:
    raise SystemExit(
        f"error: the pinned semantic-conventions-genai manifest depends on {urls or ['<nothing>']}, "
        f"but Version.props pins core {expected}.\n"
        "Bump SemConvGenAiRef and SemConvSchemaVersion together so both registries agree.")
PY
}

verify_genai_liveness() {
  local registry_json="$1"
  python3 - "${registry_json}" <<'PY'
import json
import sys

catalog = json.load(open(sys.argv[1])).get("catalog", [])
for prefix in ("gen_ai.", "mcp.", "openai.", "aws.bedrock."):
    live = sum(1 for attribute in catalog
               if attribute.get("key", "").startswith(prefix) and not attribute.get("deprecated"))
    if live == 0:
        raise SystemExit(
            f"error: merged registry has no live {prefix}* attributes — the genai registry did not "
            "contribute; refusing to emit a regressed surface")
    print(f"  live {prefix}* attributes: {live}")
PY
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
  local qyl_json="$3"
  local core_model_dir="$4"
  local genai_model_dir="$5"
  local destination="$6"
  local schema_version="$7"
  local actual_weaver_version="$8"

  python3 - "$core_json" "$genai_json" "$qyl_json" "$core_model_dir" "$genai_model_dir" "$destination" "$schema_version" "$actual_weaver_version" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

try:
    import yaml
except ImportError as exc:
    raise SystemExit("error: PyYAML is required to rehydrate event body metadata from source YAML") from exc

core_path, genai_path, qyl_path, core_model_dir, genai_model_dir, destination, schema_version, weaver_version = sys.argv[1:]
core = json.loads(Path(core_path).read_text())
genai = json.loads(Path(genai_path).read_text())
qyl = json.loads(Path(qyl_path).read_text())
QYL_SOURCE = "qyl"

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

def qyl_catalog_rows(qyl_registry):
    """qyl-owned attributes in the catalog row shape: the same key/type/brief/note/
    stability/deprecated/examples facts Weaver projects, tagged with the qyl source."""
    rows = []
    for attribute in qyl_registry.get("attributes", []):
        row = {
            "key": attribute["key"],
            "type": attribute.get("type", "string"),
            "brief": attribute.get("brief", ""),
            "note": attribute.get("note", ""),
            "stability": attribute.get("stability", "development"),
        }
        for optional in ("deprecated", "examples"):
            if optional in attribute:
                row[optional] = attribute[optional]
        row["source_registry"] = QYL_SOURCE
        rows.append(row)
    return rows

def qyl_metric_attribute(reference, catalog_by_key):
    """Resolve one qyl metric attribute reference against the merged catalog. A plain
    string is a recommended reference; an object names the key under `ref` and may
    carry `requirement_level` in Weaver's shape (string or {kind: condition})."""
    if isinstance(reference, str):
        key, requirement_level = reference, "recommended"
    else:
        key, requirement_level = reference["ref"], reference.get("requirement_level", "recommended")
    source = catalog_by_key.get(key)
    if source is None:
        raise SystemExit(f"error: qyl metric references unknown attribute {key!r}")
    row = {
        "key": key,
        "type": source["type"],
        "requirement_level": requirement_level,
        "brief": source.get("brief", ""),
        "note": source.get("note", ""),
        "stability": source.get("stability", "development"),
    }
    for optional in ("deprecated", "examples"):
        if optional in source:
            row[optional] = source[optional]
    row["source_registry"] = QYL_SOURCE
    return row

def qyl_metric_rows(qyl_registry, catalog_by_key):
    """qyl-owned metrics in the metric row shape plus the matching `groups` row, so the
    Roslyn loaders and the analyzer facts see them exactly like Weaver-projected metrics."""
    metrics, groups = [], []
    for metric in qyl_registry.get("metrics", []):
        attributes = [qyl_metric_attribute(reference, catalog_by_key) for reference in metric.get("attributes", [])]
        common = {
            "brief": metric.get("brief", ""),
            "note": metric.get("note", ""),
            "stability": metric.get("stability", "development"),
        }
        if "deprecated" in metric:
            common["deprecated"] = metric["deprecated"]
        signal = {
            "metric_name": metric["metric_name"],
            "instrument": metric["instrument"],
            "unit": metric["unit"],
            "metric_requirement_level": metric.get("metric_requirement_level", "recommended"),
            **common,
            "attribute_refs": [attribute["key"] for attribute in attributes],
            "entity_associations": list(metric.get("entity_associations", [])),
            "attributes": attributes,
            "source_registry": QYL_SOURCE,
        }
        metrics.append(signal)
        groups.append({
            "id": "metric." + metric["metric_name"],
            "type": "metric",
            "brief": common["brief"],
            "note": common["note"],
            "prefix": "",
            "stability": common["stability"],
            "metric_name": metric["metric_name"],
            "instrument": metric["instrument"],
            "unit": metric["unit"],
            "metric_requirement_level": signal["metric_requirement_level"],
            **({"deprecated": metric["deprecated"]} if "deprecated" in metric else {}),
            "attribute_refs": signal["attribute_refs"],
            "attributes": attributes,
            "source_registry": QYL_SOURCE,
        })
    return metrics, groups

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
catalog = unique_by(
    list(core.get("catalog", [])) + list(genai.get("catalog", [])) + qyl_catalog_rows(qyl),
    catalog_key,
)
catalog_by_key = {attribute["key"]: attribute for attribute in catalog}
qyl_metrics, qyl_groups = qyl_metric_rows(qyl, catalog_by_key)

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
    "groups": unique_by(list(core.get("groups", [])) + list(genai.get("groups", [])) + qyl_groups, group_key),
    "catalog": catalog,
    "metrics": unique_by(list(core.get("metrics", [])) + list(genai.get("metrics", [])) + qyl_metrics, metric_key),
    "events": events,
    "entities": unique_by(
        list(core.get("entities", [])) + list(genai.get("entities", [])),
        lambda entity: entity.get("id", "") or entity.get("name", ""),
    ),
    "scope_names": sorted(set(qyl.get("scope_names", []))),
    "event_names": sorted(set(qyl.get("event_names", []))),
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
verify_genai_core_dependency "${work_dir}/genai-source/model/manifest.yaml" "${SCHEMA_VERSION}"
genai_schema_version="$(genai_manifest_schema_version "${work_dir}/genai-source/model/manifest.yaml")"
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
  "${genai_schema_version}" \
  "${actual_weaver_version}"

merge_projected_registries \
  "${core_projection_dir}/Resources/resolved-registry.json" \
  "${genai_projection_dir}/Resources/resolved-registry.json" \
  "${qyl_registry_file}" \
  "${work_dir}/core-filtered/model" \
  "${work_dir}/genai-source/model" \
  "${output_file}" \
  "${SCHEMA_VERSION}" \
  "${actual_weaver_version}"

verify_genai_liveness "${output_file}"

python3 "${script_dir}/emit_analyzer_registry.py" --registry "${output_file}"
python3 "${script_dir}/emit_registry_resources.py" --registry "${output_file}"

echo "Regenerated ${output_file}"
echo "Regenerated registry-derived analyzer facts and public payload-schema resources"
echo "  core:  ${CORE_REF} (${core_commit})"
echo "  genai: ${GENAI_REF} (${genai_commit})"
echo "  qyl:   ${qyl_registry_file}"
echo "  weaver: ${actual_weaver_version}"
