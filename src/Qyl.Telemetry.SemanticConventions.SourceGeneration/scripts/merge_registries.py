#!/usr/bin/env python3
"""Merge the Weaver core and GenAI projections with the qyl-owned registry into the
single resolved-registry.json the generator embeds.

Three sources, one model. Rows are deduplicated per kind (group id, attribute key,
metric name, event name, entity id; the last source wins), so the qyl registry is
guarded before it is merged: every qyl attribute key must start with `qyl.` and no qyl
attribute, metric, or metric group may shadow an upstream row. A violation aborts
generation naming the offending key. `local_attribute_values` is the one sanctioned way
to touch an upstream row: it appends qyl-local members to an upstream open enum, and
fails the moment upstream lands the same value.

qyl is a real third source. Its `sources` entry has `source_ref: qyl-registry.json`
and `source_commit` = SHA-256 of that file's bytes (deterministic; no dates), and every
qyl row carries the same `source_ref` / `source_commit` with `schema_url` and
`source_date_epoch` set to JSON null: qyl publishes no schema URL.

Called by generate.sh; unit-tested by tests/scripts/test_merge_registries.py.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

try:
    import yaml
except ImportError as exc:  # pragma: no cover - environment guard
    raise SystemExit("error: PyYAML is required to rehydrate event body metadata from source YAML") from exc

QYL_SOURCE = "qyl"
QYL_SOURCE_REF = "qyl-registry.json"
QYL_NAMESPACE = "qyl."
QYL_LOCAL_VALUES = "local_attribute_values"


class MergeError(ValueError):
    """A registry input that must not be merged; the message names the offending row."""


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


def sha256_file(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


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
                "sha256": sha256_file(path),
                **source_metadata(source_registry, source_by_registry),
            })
    return files


def collect_manifests(model_dirs, source_by_registry):
    manifests = []
    for source_registry, model_dir_value in sorted(model_dirs.items()):
        path = Path(model_dir_value) / "manifest.yaml"
        if not path.is_file():
            raise MergeError(f"{source_registry} model is missing manifest.yaml: {path}")
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
            raise MergeError(
                f"type:any attribute {attribute.get('key', '<unknown>')} has no annotations.type.json_schema")
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
            raise MergeError(f"{source_registry} registry references missing JSON schemas: {', '.join(missing)}")

        for schema_path, path in sorted(discovered.items()):
            content = path.read_text()
            document = json.loads(content)
            schemas.append({
                "path": schema_path,
                "title": document.get("title", ""),
                "sha256": sha256_file(path),
                "attribute_keys": sorted(referenced_attributes.get((source_registry, schema_path), [])),
                "content": content,
                "document": document,
                **source_metadata(source_registry, source_by_registry),
            })
    return schemas


# --------------------------------------------------------------------------- #
# qyl: the third source                                                         #
# --------------------------------------------------------------------------- #
def qyl_source_entry(qyl_bytes: bytes) -> dict:
    """The `sources` entry for the qyl registry: ref is the file, commit is the SHA-256 of
    its bytes, and there is no schema URL or upstream date to cite."""
    return {
        "source_registry": QYL_SOURCE,
        "schema_url": None,
        "source_ref": QYL_SOURCE_REF,
        "source_commit": hashlib.sha256(qyl_bytes).hexdigest(),
        "source_date_epoch": None,
    }


def guard_qyl_registry(qyl_registry, core, genai):
    """Refuse a qyl row that would shadow an upstream row or leave the qyl.* namespace.
    The merge is last-wins, so without this an upstream key spelled in qyl-registry.json
    would silently replace the upstream row and drop it from the stable projection."""
    upstream = ("core", core), ("genai", genai)
    upstream_keys = {row["key"]: name for name, registry in upstream for row in registry.get("catalog", [])}
    upstream_metrics = {row["metric_name"]: name for name, registry in upstream for row in registry.get("metrics", [])}
    upstream_groups = {row["id"]: name for name, registry in upstream for row in registry.get("groups", [])}

    for attribute in qyl_registry.get("attributes", []):
        key = attribute.get("key", "")
        if not key.startswith(QYL_NAMESPACE):
            raise MergeError(
                f"qyl-registry.json attribute '{key}' is outside the qyl.* namespace; "
                "qyl-owned attributes must start with 'qyl.'")
        if key in upstream_keys:
            raise MergeError(
                f"qyl-registry.json attribute '{key}' shadows the upstream {upstream_keys[key]} catalog row of the same key")

    for metric in qyl_registry.get("metrics", []):
        name = metric.get("metric_name", "")
        if name in upstream_metrics:
            raise MergeError(
                f"qyl-registry.json metric '{name}' shadows the upstream {upstream_metrics[name]} metric of the same name")
        group_id = "metric." + name
        if group_id in upstream_groups:
            raise MergeError(
                f"qyl-registry.json metric group '{group_id}' shadows the upstream {upstream_groups[group_id]} group of the same id")

    for local in qyl_registry.get(QYL_LOCAL_VALUES, []):
        key = local.get("key", "")
        if key.startswith(QYL_NAMESPACE):
            raise MergeError(
                f"qyl-registry.json {QYL_LOCAL_VALUES} entry '{key}' names a qyl-owned attribute; "
                "declare its members inline under `attributes` instead")
        if key not in upstream_keys:
            raise MergeError(
                f"qyl-registry.json {QYL_LOCAL_VALUES} entry '{key}' names no upstream attribute")


def apply_local_attribute_values(qyl_registry, catalog_by_key, provenance):
    """Add qyl-local members to an *upstream* enum attribute.

    Some values qyl emits have no upstream spelling — the upstream attribute is an open
    enum, so the value is legal on the wire, but nothing in the pinned registry declares
    it and every registry-derived projection (analyzer enum facts, generated value sets)
    would treat it as unknown. `local_attribute_values` declares those members next to
    the upstream ones, stamped with the qyl provenance so a reader can tell them apart,
    and each carries a `note` saying it is local to qyl.

    Deletion-targeted, like the qyl.mcp.* staging namespace: the moment an upstream bump
    lands the same value the merge fails, naming it, so the local declaration is removed
    rather than silently shadowing the upstream member.
    """
    for local in qyl_registry.get(QYL_LOCAL_VALUES, []):
        attribute = catalog_by_key[local["key"]]
        raw_type = attribute.get("type")
        if not isinstance(raw_type, dict) or not isinstance(raw_type.get("members"), list):
            raise MergeError(
                f"qyl-registry.json {QYL_LOCAL_VALUES} entry '{local['key']}' names a non-enum upstream attribute")
        upstream_values = {str(member.get("value")) for member in raw_type["members"]}
        for member in local.get("members", []):
            if str(member["value"]) in upstream_values:
                raise MergeError(
                    f"qyl-registry.json {QYL_LOCAL_VALUES} member '{local['key']}={member['value']}' "
                    "now exists upstream; delete the local declaration")
            raw_type["members"].append({**member, **provenance})
    return catalog_by_key


def qyl_catalog_rows(qyl_registry, provenance):
    """qyl-owned attributes in the catalog row shape: the same key/type/brief/note/
    stability/deprecated/examples facts Weaver projects, stamped with the qyl source."""
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
        row.update(provenance)
        rows.append(row)
    return rows


def qyl_metric_attribute(reference, catalog_by_key, provenance):
    """Resolve one qyl metric attribute reference against the merged catalog. A plain
    string is a recommended reference; an object names the key under `ref` and may
    carry `requirement_level` in Weaver's shape (string or {kind: condition})."""
    if isinstance(reference, str):
        key, requirement_level = reference, "recommended"
    else:
        key, requirement_level = reference["ref"], reference.get("requirement_level", "recommended")
    source = catalog_by_key.get(key)
    if source is None:
        raise MergeError(f"qyl metric references unknown attribute {key!r}")
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
    row.update(provenance)
    return row


def qyl_metric_rows(qyl_registry, catalog_by_key, provenance):
    """qyl-owned metrics in the metric row shape plus the matching `groups` row, so the
    Roslyn loaders and the analyzer facts see them exactly like Weaver-projected metrics."""
    metrics, groups = [], []
    for metric in qyl_registry.get("metrics", []):
        attributes = [qyl_metric_attribute(reference, catalog_by_key, provenance) for reference in metric.get("attributes", [])]
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
            **provenance,
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
            **provenance,
        })
    return metrics, groups


def merge(core, genai, qyl, qyl_bytes, core_model_dir, genai_model_dir, schema_version, weaver_version):
    guard_qyl_registry(qyl, core, genai)

    qyl_source = qyl_source_entry(qyl_bytes)
    provenance = dict(qyl_source)

    sources = unique_by(
        list(core.get("sources", [])) + list(genai.get("sources", [])) + [qyl_source],
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
        list(core.get("catalog", [])) + list(genai.get("catalog", [])) + qyl_catalog_rows(qyl, provenance),
        catalog_key,
    )
    catalog_by_key = {attribute["key"]: attribute for attribute in catalog}
    apply_local_attribute_values(qyl, catalog_by_key, provenance)
    qyl_metrics, qyl_groups = qyl_metric_rows(qyl, catalog_by_key, provenance)

    return {
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


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--core", type=Path, required=True, help="Weaver core projection JSON")
    parser.add_argument("--genai", type=Path, required=True, help="Weaver GenAI projection JSON")
    parser.add_argument("--qyl", type=Path, required=True, help="qyl-registry.json")
    parser.add_argument("--core-model", type=Path, required=True)
    parser.add_argument("--genai-model", type=Path, required=True)
    parser.add_argument("--schema-version", required=True)
    parser.add_argument("--weaver-version", required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)

    qyl_bytes = args.qyl.read_bytes()
    try:
        merged = merge(
            json.loads(args.core.read_text()),
            json.loads(args.genai.read_text()),
            json.loads(qyl_bytes),
            qyl_bytes,
            args.core_model,
            args.genai_model,
            args.schema_version,
            args.weaver_version,
        )
    except MergeError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(merged, indent=2, ensure_ascii=False) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
