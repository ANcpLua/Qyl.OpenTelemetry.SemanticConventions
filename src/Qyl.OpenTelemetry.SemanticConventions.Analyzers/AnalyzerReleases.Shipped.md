; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 3.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
QYL0001 | OpenTelemetry.SemanticConventions | Info | graphql.document is opt-in
QYL0002 | OpenTelemetry.SemanticConventions | Warning | RPC server span must not include client.address / client.port
QYL0003 | OpenTelemetry.SemanticConventions | Warning | Deprecated semantic-convention constant
QYL0004 | OpenTelemetry.SemanticConventions | Info | Prefer typed semantic-convention constant over string literal
QYL0005 | OpenTelemetry.SemanticConventions | Warning | String literal matches a deprecated semantic-convention name
QYL0006 | OpenTelemetry | Warning | Missing telemetry schema URL
QYL0007 | OpenTelemetry.SemanticConventions | Warning | Deprecated semantic-convention value
QYL0008 | OpenTelemetry.SemanticConventions | Warning | Incubating semantic-convention member used in a library
QYL0009 | OpenTelemetry.SemanticConventions | Error | Obsolete semantic convention has an exact replacement
QYL0010 | OpenTelemetry.SemanticConventions | Warning | Semantic convention migration needs review
QYL0011 | OpenTelemetry.SemanticConventions | Info | Legacy semantic convention appears in compatibility or test code
QYL0012 | OpenTelemetry | Error | Invalid attribute value
QYL0013 | OpenTelemetry | Warning | Incorrect attribute type
QYL0100 | OpenTelemetry | Warning | Activity/Span missing semantic convention attributes
QYL0101 | OpenTelemetry | Warning | ActivitySource should be registered with AddSource()
QYL0102 | OpenTelemetry | Error | [Traced] attribute requires non-empty ActivitySourceName
QYL0103 | OpenTelemetry | Warning | Duplicate instrumentation detected
QYL0104 | OpenTelemetry | Error | Invalid ActivitySource name
QYL0105 | OpenTelemetry | Info | Manual span recommended
QYL0106 | OpenTelemetry | Warning | Orphaned [TracedTag]
QYL0107 | OpenTelemetry | Info | Redundant [NoTrace]
QYL0108 | OpenTelemetry | Warning | Non-interceptable [Traced]
QYL0109 | OpenTelemetry | Error | [TracedTag] on out/ref parameter
QYL0110 | OpenTelemetry | Warning | Missing exception recording on Activity error status
QYL0200 | Metrics | Warning | Meter should be registered with AddMeter()
QYL0201 | Metrics | Warning | Metric instrument name should follow naming conventions
QYL0202 | Metrics | Warning | High-cardinality tag on metric
QYL0300 | Configuration | Warning | ServiceDefaults configuration incomplete
QYL0301 | Configuration | Warning | Collector endpoint should use OTLP protocol
QYL0302 | OpenTelemetry | Warning | Missing OpenTelemetry configuration
QYL0303 | OpenTelemetry | Warning | Missing resource attributes
QYL0304 | Configuration | Warning | Enable EventSourceSupport for AOT with telemetry
QYL0305 | OpenTelemetry | Warning | Replace legacy SemanticConventions accessor
QYL0400 | OpenTelemetry.SemanticConventions | Warning | gen_ai.execute_tool span requires gen_ai.tool.name
QYL0401 | GenAI | Warning | GenAI span missing required attributes
QYL0402 | GenAI | Warning | Use gen_ai.client.token.usage histogram for token metrics
QYL0403 | GenAI | Warning | GenAI operation name should follow semantic conventions
QYL0404 | GenAI | Warning | Deprecated GenAI semantic convention
QYL0405 | GenAI | Warning | Non-interceptable [AgentTraced]
QYL0406 | GenAI | Warning | Direct GenAI SDK call bypasses automatic OTel instrumentation
QYL0500 | Metrics | Error | [Meter] class must be partial static
QYL0501 | Metrics | Error | Metric method must be partial
QYL0600 | Configuration | Warning | Insecure endpoint
QYL0601 | OpenTelemetry | Warning | Sensitive data in span attribute
QYL0700 | OpenTelemetry | Warning | Missing OTLP configuration
QYL0701 | OpenTelemetry | Warning | Uncompressed OTLP export
QYL0702 | OpenTelemetry | Warning | Batch export disabled
QYL0703 | OpenTelemetry | Info | Consider configuring sampling
