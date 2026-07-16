#!/usr/bin/env python3
"""Generate compact analyzer facts from the complete resolved registry."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[2]
DEFAULT_REGISTRY = SCRIPT_DIR.parent / "Resources" / "resolved-registry.json"
DEFAULT_OUTPUT = (
    REPO_ROOT
    / "src"
    / "Qyl.OpenTelemetry.SemanticConventions.Analyzers"
    / "SemconvRegistryFacts.g.cs"
)


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


def value_kind(raw_type: object) -> tuple[str, bool]:
    if isinstance(raw_type, dict) and isinstance(raw_type.get("members"), list):
        return "String", False
    if not isinstance(raw_type, str):
        return "Unknown", False

    is_template = raw_type.startswith("template[") and raw_type.endswith("]")
    primitive = raw_type[len("template["):-1] if is_template else raw_type
    kind = {
        "string": "String",
        "int": "Integer",
        "double": "Double",
        "boolean": "Boolean",
        "string[]": "StringArray",
        "int[]": "IntegerArray",
        "double[]": "DoubleArray",
        "boolean[]": "BooleanArray",
        "any": "Any",
    }.get(primitive, "Unknown")
    return kind, is_template


def enum_values(attribute: dict) -> list[str]:
    raw_type = attribute.get("type")
    if not isinstance(raw_type, dict):
        return []
    return sorted({str(member["value"]) for member in raw_type.get("members", []) if "value" in member})


def required_attributes(group: dict) -> list[str]:
    return sorted(
        attribute["key"]
        for attribute in group.get("attributes", [])
        if attribute.get("requirement_level") == "required"
    )


def discriminator_attribute(group: dict) -> dict | None:
    candidates = []
    for attribute in group.get("attributes", []):
        key = attribute.get("key", "")
        if (key.endswith(".operation.name") or key.endswith(".method.name")) \
                and attribute.get("requirement_level") == "required":
            if enum_values(attribute):
                candidates.append(attribute)
    if len(candidates) > 1:
        raise ValueError(f"span group {group['id']} has multiple operation discriminators")
    return candidates[0] if candidates else None


def operation_segment(group_id: str) -> str:
    parts = group_id.split(".")
    return parts[-2] if len(parts) >= 2 else group_id


def assign_operations(groups: list[dict]) -> dict[str, list[str]]:
    assignments: dict[str, list[str]] = {}
    groups_by_discriminator: dict[str, list[tuple[dict, dict]]] = {}
    for group in groups:
        discriminator = discriminator_attribute(group)
        if discriminator is not None:
            groups_by_discriminator.setdefault(discriminator["key"], []).append((group, discriminator))

    for discriminator_key, entries in groups_by_discriminator.items():
        all_values = sorted({value for _, attribute in entries for value in enum_values(attribute)})
        if discriminator_key.endswith(".method.name"):
            for group, _ in entries:
                assignments[group["id"]] = all_values
            continue

        assigned_values: set[str] = set()
        unmatched: list[dict] = []
        for group, _ in entries:
            segment = operation_segment(group["id"])
            matched = [
                value for value in all_values
                if value == segment or segment in value.split("_")
            ]
            if matched:
                assignments[group["id"]] = matched
                assigned_values.update(matched)
            else:
                unmatched.append(group)

        remaining = sorted(set(all_values) - assigned_values)
        if remaining:
            if len(unmatched) != 1:
                ids = ", ".join(group["id"] for group in unmatched)
                raise ValueError(
                    f"cannot derive {discriminator_key} span mapping for remaining values {remaining}; unmatched groups: {ids}")
            assignments[unmatched[0]["id"]] = remaining
        elif unmatched:
            raise ValueError(
                f"operation discriminator {discriminator_key} has unmatched groups but no remaining values")
    return assignments


CONSTRAINT_PATTERN = re.compile(
    r"`(?P<key>[^`]+)`\s+MUST be set to\s+`\\?\"(?P<value>[^\"`]+)\\?\"`",
    re.IGNORECASE,
)


def refinement_constraint(group: dict) -> tuple[str, str] | None:
    match = CONSTRAINT_PATTERN.search(group.get("note", ""))
    if not match:
        return None
    return match.group("key"), match.group("value")


def build_span_rules(registry: dict) -> list[dict]:
    base_groups = [
        group for group in registry["groups"]
        if group.get("source_registry") == "genai" and group.get("type") == "span"
    ]
    assignments = assign_operations(base_groups)
    rules: list[dict] = []
    for group in base_groups:
        discriminator = discriminator_attribute(group)
        if discriminator is None:
            continue
        rules.append({
            "id": group["id"],
            "kind": group.get("span_kind", "internal"),
            "discriminator_key": discriminator["key"],
            "discriminator_values": assignments[group["id"]],
            "constraint_key": None,
            "constraint_value": None,
            "required_attributes": required_attributes(group),
        })

    base_by_suffix = {
        (operation_segment(rule["id"]), rule["kind"]): rule
        for rule in rules
        if rule["discriminator_key"].endswith(".operation.name")
    }
    base_ids = {group["id"] for group in base_groups}
    refinements = [
        group for group in registry["groups"]
        if group.get("source_registry") == "genai"
        and group.get("type") == "span_refinement"
        and group.get("id") not in base_ids
    ]
    for group in refinements:
        constraint = refinement_constraint(group)
        if constraint is None:
            continue
        suffix = (operation_segment(group["id"]), group.get("span_kind", "internal"))
        if suffix not in base_by_suffix:
            raise ValueError(f"cannot find base span rule for refinement {group['id']}")
        base = base_by_suffix[suffix]
        rules.append({
            "id": group["id"],
            "kind": base["kind"],
            "discriminator_key": base["discriminator_key"],
            "discriminator_values": base["discriminator_values"],
            "constraint_key": constraint[0],
            "constraint_value": constraint[1],
            "required_attributes": required_attributes(group),
        })

    rules.sort(key=lambda rule: (rule["constraint_key"] is None, rule["id"]))
    return rules


def render_string_array(values: list[str]) -> str:
    return "new[] { " + ", ".join(csharp_string(value) for value in values) + " }"


def generate(registry: dict) -> str:
    attributes = sorted(registry["catalog"], key=lambda attribute: attribute["key"])
    exact_types: list[tuple[str, str]] = []
    template_types: list[tuple[str, str]] = []
    enums: list[tuple[str, list[str]]] = []
    for attribute in attributes:
        kind, is_template = value_kind(attribute.get("type"))
        (template_types if is_template else exact_types).append((attribute["key"], kind))
        values = enum_values(attribute)
        if values:
            enums.append((attribute["key"], values))

    span_rules = build_span_rules(registry)
    execute_tool_rules = [
        rule for rule in span_rules
        if rule["kind"] == "internal"
        and rule["constraint_key"] is None
        and "execute_tool" in rule["discriminator_values"]
    ]
    if len(execute_tool_rules) != 1:
        raise ValueError(f"expected exactly one unconstrained execute_tool span rule, found {execute_tool_rules}")
    execute_tool_rule = execute_tool_rules[0]
    execute_tool_required = [
        key for key in execute_tool_rule["required_attributes"]
        if key != execute_tool_rule["discriminator_key"]
    ]
    if len(execute_tool_required) != 1:
        raise ValueError(
            "expected execute_tool span to have exactly one required attribute besides its discriminator, "
            f"found {execute_tool_required}")
    genai_metrics = sorted({
        metric["metric_name"]
        for metric in registry["metrics"]
        if metric.get("source_registry") == "genai"
    })
    token_usage_metrics = [name for name in genai_metrics if name.endswith(".token.usage")]
    if len(token_usage_metrics) != 1:
        raise ValueError(f"expected exactly one GenAI token usage metric, found {token_usage_metrics}")
    genai_discriminators = sorted({
        rule["discriminator_key"]
        for rule in span_rules
        if rule["discriminator_key"].startswith("gen_ai.")
    })

    source = next(item for item in registry["sources"] if item["source_registry"] == "genai")
    lines = [
        "// <auto-generated/>",
        f"// Generated from resolved-registry.json (semantic-conventions-genai@{source['source_commit']}).",
        "// Regenerate with scripts/emit_analyzer_registry.py; do not edit by hand.",
        "// </auto-generated>",
        "",
        "#nullable enable",
        "",
        "namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;",
        "",
        "internal enum SemconvAttributeValueKind",
        "{",
        "    Unknown,",
        "    String,",
        "    Integer,",
        "    Double,",
        "    Boolean,",
        "    StringArray,",
        "    IntegerArray,",
        "    DoubleArray,",
        "    BooleanArray,",
        "    Any",
        "}",
        "",
        "internal sealed class SemconvSpanRule(",
        "    string id,",
        "    string kind,",
        "    string discriminatorKey,",
        "    string[] discriminatorValues,",
        "    string? constraintKey,",
        "    string? constraintValue,",
        "    string[] requiredAttributes)",
        "{",
        "    internal string Id { get; } = id;",
        "    internal string Kind { get; } = kind;",
        "    internal string DiscriminatorKey { get; } = discriminatorKey;",
        "    internal string[] DiscriminatorValues { get; } = discriminatorValues;",
        "    internal string? ConstraintKey { get; } = constraintKey;",
        "    internal string? ConstraintValue { get; } = constraintValue;",
        "    internal string[] RequiredAttributes { get; } = requiredAttributes;",
        "}",
        "",
        "internal static class SemconvRegistryFacts",
        "{",
        f"    internal const string GenAiTokenUsageMetricName = {csharp_string(token_usage_metrics[0])};",
        f"    internal const string ExecuteToolOperationKey = {csharp_string(execute_tool_rule['discriminator_key'])};",
        "    internal const string ExecuteToolOperationValue = \"execute_tool\";",
        f"    internal const string ExecuteToolRequiredAttribute = {csharp_string(execute_tool_required[0])};",
        "",
        "    private static readonly Dictionary<string, SemconvAttributeValueKind> AttributeTypes =",
        "        new(StringComparer.Ordinal)",
        "        {",
    ]
    for key, kind in exact_types:
        lines.append(f"            [{csharp_string(key)}] = SemconvAttributeValueKind.{kind},")
    lines.extend([
        "        };",
        "",
        "    private static readonly (string Prefix, SemconvAttributeValueKind Kind)[] TemplateAttributeTypes =",
        "    {",
    ])
    for key, kind in template_types:
        lines.append(f"        ({csharp_string(key + '.')}, SemconvAttributeValueKind.{kind}),")
    lines.extend([
        "    };",
        "",
        "    private static readonly Dictionary<string, string[]> EnumValues =",
        "        new(StringComparer.Ordinal)",
        "        {",
    ])
    for key, values in enums:
        lines.append(f"            [{csharp_string(key)}] = {render_string_array(values)},")
    lines.extend([
        "        };",
        "",
        "    private static readonly HashSet<string> GenAiSpanDiscriminatorKeys =",
        f"        new({render_string_array(genai_discriminators)}, StringComparer.Ordinal);",
        "",
        "    private static readonly HashSet<string> GenAiMetricNames =",
        f"        new({render_string_array(genai_metrics)}, StringComparer.Ordinal);",
        "",
        "    internal static readonly SemconvSpanRule[] SpanRules =",
        "    {",
    ])
    for rule in span_rules:
        constraint_key = "null" if rule["constraint_key"] is None else csharp_string(rule["constraint_key"])
        constraint_value = "null" if rule["constraint_value"] is None else csharp_string(rule["constraint_value"])
        lines.extend([
            "        new(",
            f"            {csharp_string(rule['id'])},",
            f"            {csharp_string(rule['kind'])},",
            f"            {csharp_string(rule['discriminator_key'])},",
            f"            {render_string_array(rule['discriminator_values'])},",
            f"            {constraint_key},",
            f"            {constraint_value},",
            f"            {render_string_array(rule['required_attributes'])}),",
        ])
    lines.extend([
        "    };",
        "",
        "    internal static bool TryGetAttributeType(string key, out SemconvAttributeValueKind kind)",
        "    {",
        "        if (AttributeTypes.TryGetValue(key, out kind))",
        "            return true;",
        "",
        "        foreach (var template in TemplateAttributeTypes)",
        "        {",
        "            if (key.StartsWith(template.Prefix, StringComparison.Ordinal) && key.Length > template.Prefix.Length)",
        "            {",
        "                kind = template.Kind;",
        "                return true;",
        "            }",
        "        }",
        "",
        "        kind = SemconvAttributeValueKind.Unknown;",
        "        return false;",
        "    }",
        "",
        "    internal static bool TryGetCanonicalEnumValue(string key, string value, out string? canonical)",
        "    {",
        "        canonical = null;",
        "        if (!EnumValues.TryGetValue(key, out var values))",
        "            return false;",
        "",
        "        foreach (var candidate in values)",
        "        {",
        "            if (string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase))",
        "            {",
        "                canonical = candidate;",
        "                return true;",
        "            }",
        "        }",
        "",
        "        return false;",
        "    }",
        "",
        "    internal static bool IsGenAiSpanDiscriminator(string key) => GenAiSpanDiscriminatorKeys.Contains(key);",
        "",
        "    internal static bool IsKnownGenAiMetric(string name) => GenAiMetricNames.Contains(name);",
        "",
        "    internal static bool TryResolveSpanRule(",
        "        IReadOnlyDictionary<string, string> attributes,",
        "        string spanKind,",
        "        out SemconvSpanRule? rule)",
        "    {",
        "        foreach (var candidate in SpanRules)",
        "        {",
        "            if (!string.Equals(candidate.Kind, spanKind, StringComparison.OrdinalIgnoreCase)",
        "                || !attributes.TryGetValue(candidate.DiscriminatorKey, out var discriminatorValue)",
        "                || !candidate.DiscriminatorValues.Contains(discriminatorValue, StringComparer.Ordinal))",
        "            {",
        "                continue;",
        "            }",
        "",
        "            if (candidate.ConstraintKey is not null",
        "                && (!attributes.TryGetValue(candidate.ConstraintKey, out var constraintValue)",
        "                    || !string.Equals(constraintValue, candidate.ConstraintValue, StringComparison.Ordinal)))",
        "            {",
        "                continue;",
        "            }",
        "",
        "            rule = candidate;",
        "            return true;",
        "        }",
        "",
        "        rule = null;",
        "        return false;",
        "    }",
        "}",
        "",
    ])
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--registry", type=Path, default=DEFAULT_REGISTRY)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()

    expected = generate(json.loads(args.registry.read_text()))
    if args.check:
        if not args.output.is_file() or args.output.read_text() != expected:
            print(f"stale generated analyzer registry: {args.output}", file=sys.stderr)
            return 1
        return 0

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(expected)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
