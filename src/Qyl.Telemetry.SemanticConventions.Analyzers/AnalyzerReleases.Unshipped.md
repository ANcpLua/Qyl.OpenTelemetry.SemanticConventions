; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
QYL0006 | OpenTelemetry | Info | OpenTelemetry | Warning | Records the Info severity the analyzer code, rule catalog and docs carry; the 3.0.0 entry listed Warning, [Documentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/blob/main/docs/rules/QYL0006_MissingSchemaUrl.md)
QYL0200 | OpenTelemetry | Error | Metrics | Warning | Rewritten to the qyl G1 gate: telemetry names in name positions must be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/blob/main/docs/rules/QYL0200_TelemetryName.md)
QYL0201 | Metrics | Error | Metrics | Warning | Metric descriptor names must additionally be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/blob/main/docs/rules/QYL0201_InvalidMetricName.md)
