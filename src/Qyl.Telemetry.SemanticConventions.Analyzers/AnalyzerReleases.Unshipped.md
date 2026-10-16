; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
QYL0112 | OpenTelemetry | Error | Qyl0112NonConstantTelemetryNameAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0112_NonConstantTelemetryName.md)
QYL0120 | OpenTelemetry | Error | Qyl0120ContribInstrumentationWithAddQylAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0120_ContribInstrumentationWithAddQyl.md)
QYL0121 | OpenTelemetry | Error | Qyl0121CustomValueEqualsMemberAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0121_CustomValueEqualsMember.md)
QYL0122 | GenAI | Error | Qyl0122StarterInsideAiFunctionAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0122_StarterInsideAiFunction.md)

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
QYL0006 | OpenTelemetry | Info | OpenTelemetry | Warning | Replaced the blanket missing-schema heuristic with a diagnostic for telemetry.schema_url resource attributes; unknown schemas remain valid, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0006_SchemaUrlAttribute.md)
QYL0110 | OpenTelemetry | Error | OpenTelemetry | Warning | Rewritten for the generated starters: a catch that throws inside a starter's scope without RecordFailure, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0110_MissingRecordFailure.md)
QYL0200 | OpenTelemetry | Error | Metrics | Warning | Reserve qyl-owned names while allowing custom application and library telemetry, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0200_TelemetryName.md)
