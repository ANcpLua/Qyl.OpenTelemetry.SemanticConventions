; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
QYL0111 | OpenTelemetry | Error | Qyl0111RawActivitySourceAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0111_RawActivitySource.md)
QYL0112 | OpenTelemetry | Error | Qyl0112NonConstantTelemetryNameAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0112_NonConstantTelemetryName.md)
QYL0113 | OpenTelemetry | Error | Qyl0113DeniedAppTagKeyAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0113_DeniedAppTagKey.md)
QYL0120 | OpenTelemetry | Error | Qyl0120ContribInstrumentationWithAddQylAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0120_ContribInstrumentationWithAddQyl.md)
QYL0121 | OpenTelemetry | Error | Qyl0121CustomValueEqualsMemberAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0121_CustomValueEqualsMember.md)
QYL0122 | GenAI | Error | Qyl0122StarterInsideAIFunctionAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0122_StarterInsideAIFunction.md)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
QYL0006 | OpenTelemetry | Info | OpenTelemetry | Warning | Records the Info severity the analyzer code, rule catalog and docs carry; the 3.0.0 entry listed Warning, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0006_MissingSchemaUrl.md)
QYL0110 | OpenTelemetry | Error | OpenTelemetry | Warning | Rewritten for the generated starters: a catch that throws inside a starter's scope without RecordFailure, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0110_MissingRecordFailure.md)
QYL0200 | OpenTelemetry | Error | Metrics | Warning | Rewritten to the qyl G1 gate: telemetry names in name positions must be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0200_TelemetryName.md)
QYL0201 | Metrics | Error | Metrics | Warning | Metric descriptor names must additionally be members of the generated registry catalog, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0201_InvalidMetricName.md)
