; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
QYL0200 | OpenTelemetry | Error | Metrics | Warning | Rewritten to the qyl G1 gate: telemetry names in name positions must be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/blob/main/docs/rules/QYL0200_TelemetryName.md)
QYL0201 | Metrics | Error | Metrics | Warning | Metric descriptor names must additionally be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/blob/main/docs/rules/QYL0201_InvalidMetricName.md)
