; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 2.0.0

### New Rules

Rule ID  | Category                          | Severity | Notes
---------|-----------------------------------|----------|----------------------------------------------------------------------------------
QYL0001  | OpenTelemetry.SemanticConventions | Warning  | gen_ai.execute_tool span requires gen_ai.tool.name
QYL0002  | OpenTelemetry.SemanticConventions | Info     | graphql.document is opt-in
QYL0003  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0004  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0005  | OpenTelemetry.SemanticConventions | Warning  | RPC server span must not include client.address / client.port
QYL0006  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0007  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0008  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0009  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0010  | OpenTelemetry.SemanticConventions | Warning  | Deprecated semantic-convention constant
QYL0011  | OpenTelemetry.SemanticConventions | Info     | Prefer typed semantic-convention constant over string literal
QYL0012  | OpenTelemetry.SemanticConventions | Warning  | String literal matches a deprecated semantic-convention name
QYL0013  | OpenTelemetry                     | Warning  | Detects OpenTelemetry configurations that don't set the schema URL
QYL0014  | OpenTelemetry.SemanticConventions | Warning  | Deprecated semantic-convention value
QYL0015  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0016  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0017  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0018  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0019  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0020  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0021  | OpenTelemetry.SemanticConventions | Warning  | Incubating semantic-convention member used in a library
QYL0022  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0023  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0024  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0025  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0026  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0027  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0028  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0029  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0030  | OpenTelemetry.SemanticConventions | Error    | Obsolete semantic convention has an exact replacement
QYL0031  | OpenTelemetry.SemanticConventions | Warning  | Semantic convention migration needs review
QYL0032  | OpenTelemetry.SemanticConventions | Info     | Legacy semantic convention appears in compatibility or test code
QYL0033  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0034  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0035  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0036  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0037  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0038  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0039  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0040  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0041  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0042  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0043  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0044  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0045  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0046  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0047  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0048  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0049  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0050  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0051  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0052  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0053  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0054  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0055  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0056  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0057  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0058  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0059  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0060  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0061  | OpenTelemetry                     | Warning  | Detects Activity/Span creation without semantic convention attributes
QYL0062  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0063  | OpenTelemetry                     | Warning  | Detects ActivitySource instances that are not registered with AddSource()
QYL0064  | GenAI                             | Warning  | Detects GenAI spans that are missing required semantic convention attributes
QYL0065  | GenAI                             | Warning  | Detects token usage metrics that don't use the standard histogram
QYL0066  | GenAI                             | Warning  | Detects GenAI operation names that don't follow semantic conventions
QYL0067  | Metrics                           | Warning  | Detects Meter instances that are not registered with AddMeter() anywhere in the compilation
QYL0068  | Metrics                           | Warning  | Detects metric instrument names that don't follow naming conventions
QYL0069  | Configuration                     | Warning  | Detects incomplete ServiceDefaults configuration
QYL0070  | Configuration                     | Warning  | Detects collector endpoint configurations that don't use OTLP protocol
QYL0071  | Metrics                           | Error    | Detects [Meter] classes that are not declared as partial static
QYL0072  | Metrics                           | Error    | Detects [Counter]/[Histogram] methods that are not declared as partial
QYL0073  | OpenTelemetry                     | Error    | Validates [Traced] attribute has non-empty ActivitySourceName
QYL0074  | GenAI                             | Warning  | Detects deprecated GenAI semantic convention attribute names
QYL0075  | Metrics                           | Warning  | Warns about high-cardinality tags on metrics
QYL0076  | OpenTelemetry                     | Warning  | Detects when AddServiceDefaults() or similar setup is called but AddOpenTelemetry() is missing
QYL0077  | OpenTelemetry                     | Warning  | Detects duplicate instrumentation - methods with both auto-instrumentation and manual spans
QYL0078  | OpenTelemetry                     | Error    | Detects ActivitySource names that don't follow reverse-DNS naming convention
QYL0079  | OpenTelemetry                     | Info     | Detects complex async patterns in [Traced] methods where manual instrumentation
QYL0080  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0081  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0082  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0083  | Configuration                     | Warning  | Detects HTTP endpoints used where HTTPS is expected
QYL0084  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0085  | OpenTelemetry                     | Error    | Detects attribute values that violate OTel semantic convention specifications
QYL0086  | OpenTelemetry                     | Warning  | Detects OpenTelemetry attributes set with incorrect types
QYL0087  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0088  | OpenTelemetry                     | Warning  | Detects potential PII or credential data in span attributes
QYL0089  | OpenTelemetry                     | Warning  | Detects OTLP exporter calls without explicit endpoint configuration
QYL0090  | OpenTelemetry                     | Warning  | Detects OTLP exporter configurations using HTTP protocol without compression
QYL0091  | OpenTelemetry                     | Warning  | Detects usage of SimpleSpanProcessor or SimpleActivityExportProcessor which exports
QYL0092  | OpenTelemetry                     | Info     | Detects OpenTelemetry tracing configurations without sampling configured
QYL0093  | OpenTelemetry                     | Warning  | Detects when OpenTelemetry is configured without essential resource attributes
QYL0094  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0095  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0096  | Configuration                     | Warning  | Enable EventSourceSupport for AOT with telemetry
QYL0097  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0098  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0099  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0100  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0101  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0102  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0103  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0104  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0105  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0106  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0107  | OpenTelemetry                     | Warning  | Detects [TracedTag] on parameters where neither the method nor its declaring type has [Traced]
QYL0108  | OpenTelemetry                     | Info     | Detects [NoTrace] on a method whose declaring type has no class-level [Traced]
QYL0109  | OpenTelemetry                     | Warning  | Detects [Traced] on abstract, extern, or partial definition methods that cannot be intercepted
QYL0110  | OpenTelemetry                     | Error    | Detects [TracedTag] on parameters with out or ref modifiers
QYL0111  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0112  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0113  | OpenTelemetry                     | Warning  | Detects Activity.SetStatus(Error) inside catch blocks without recording the exception
QYL0114  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0115  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0116  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0117  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0118  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0119  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0120  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0121  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0122  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0123  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0124  | GenAI                             | Warning  | Detects [AgentTraced] on abstract, extern, or partial definition methods that cannot be intercepted
QYL0125  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0126  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0127  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0131  | GenAI                             | Warning  | Warns when application code calls GenAI SDK APIs directly, bypassing the
QYL0132  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0133  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0134  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0135  | OpenTelemetry                     | Warning  | Flags usage of the legacy aggregated semantic-convention accessor types
QYL0136  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0137  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0138  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0139  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0140  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0141  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0142  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0143  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0144  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0145  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0146  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0147  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0148  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0149  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0150  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0151  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0152  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0153  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0154  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0155  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
QYL0156  | OpenTelemetry.SemanticConventions | Disabled | Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.
