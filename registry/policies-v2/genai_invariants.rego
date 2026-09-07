package after_resolution

import rego.v1

# Checked against the materialized v2 registry (`--v2 --include-unreferenced`). These are the
# assumptions the generated analyzer facts are derived under: emit_analyzer_registry.py used to
# raise on each of them, and the template's JQ filter would now produce a silently wrong table
# instead.

genai_schema_prefix := "https://opentelemetry.io/schemas/gen-ai-dev/"

genai_metric_names contains name if {
	some metric in input.registry.metrics
	startswith(object.get(metric, ["provenance", "source"], ""), genai_schema_prefix)
	name := metric.name
}

token_usage_metrics contains name if {
	some name in genai_metric_names
	endswith(name, ".token.usage")
}

deny contains genai_violation(description, "-") if {
	count(genai_metric_names) > 0
	count(token_usage_metrics) != 1
	description := sprintf(
		"expected exactly one GenAI token usage metric, found %v. QYL0402 rewrites a counter into that one histogram and cannot choose between two.",
		[sort([n | some n in token_usage_metrics])],
	)
}

# A GenAI `any` attribute is a structured payload; the JSON Schema of that payload is what the
# Incubating package used to ship and what a consumer validates against.
deny contains genai_violation(description, attr.key) if {
	some attr in input.registry.attributes
	startswith(object.get(attr, ["provenance", "source"], ""), genai_schema_prefix)
	attr.type == "any"
	not attr.annotations.type.json_schema
	description := sprintf(
		"GenAI attribute '%s' is typed `any` but carries no annotations.type.json_schema; its payload shape would be undefined.",
		[attr.key],
	)
}

# The one span the analyzers hard-code: QYL0400 and QYL0401 resolve `execute_tool` against a
# single unconstrained internal span rule.
execute_tool_spans contains span.type if {
	some span in input.registry.spans
	startswith(object.get(span, ["provenance", "source"], ""), genai_schema_prefix)
	span.kind == "internal"
	parts := split(span.type, ".")
	parts[count(parts) - 2] == "execute_tool"
	some attr in span.attributes
	endswith(attr.key, ".operation.name")
	attr.requirement_level == "required"
	some member in attr.type.members
	member.value == "execute_tool"
}

deny contains genai_violation(description, "-") if {
	count(genai_metric_names) > 0
	count(execute_tool_spans) != 1
	description := sprintf(
		"expected exactly one unconstrained execute_tool span, found %v. QYL0400 and QYL0401 resolve execute_tool against a single internal span rule.",
		[sort([s | some s in execute_tool_spans])],
	)
}

genai_violation(description, attr) := {
	"id": description,
	"type": "semconv_attribute",
	"category": "genai_invariants",
	"group": "gen_ai",
	"attr": attr,
}
