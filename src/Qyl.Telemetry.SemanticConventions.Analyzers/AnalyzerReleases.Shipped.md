; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 3.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
QYL0001 | OpenTelemetry.SemanticConventions | Info | GraphqlDocumentOptInAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0001_GraphqlDocumentOptIn.md)
QYL0002 | OpenTelemetry.SemanticConventions | Warning | RpcServerClientAttributeAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0002_RpcServerClientAttribute.md)
QYL0003 | OpenTelemetry.SemanticConventions | Warning | DeprecatedSemconvAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0003_DeprecatedSemconv.md)
QYL0004 | OpenTelemetry.SemanticConventions | Info | PreferSemconvConstantAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0004_PreferSemconvConstant.md)
QYL0005 | OpenTelemetry.SemanticConventions | Warning | LiteralMatchesDeprecatedSemconvAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0005_LiteralMatchesDeprecatedSemconv.md)
QYL0006 | OpenTelemetry | Warning | Qyl0006MissingSchemaUrlAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0006_MissingSchemaUrl.md)
QYL0007 | OpenTelemetry.SemanticConventions | Warning | DeprecatedSemconvValueAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0007_DeprecatedSemconvValue.md)
QYL0008 | OpenTelemetry.SemanticConventions | Warning | IncubatingSemconvInLibraryAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0008_IncubatingSemconvInLibrary.md)
QYL0009 | OpenTelemetry.SemanticConventions | Error | SupplementalSemconvMigrationAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0009_SupplementalSemconvMigration.md)
QYL0010 | OpenTelemetry.SemanticConventions | Warning | SupplementalSemconvMigrationAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0010_SupplementalSemconvMigration.md)
QYL0012 | OpenTelemetry | Error | Qyl0012InvalidAttributeValueAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0012_InvalidAttributeValue.md)
QYL0013 | OpenTelemetry | Warning | Qyl0013IncorrectAttributeTypeAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0013_IncorrectAttributeType.md)
QYL0110 | OpenTelemetry | Warning | Qyl0110MissingRecordFailureAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0110_MissingRecordFailure.md)
QYL0200 | Metrics | Warning | Qyl0200TelemetryNameAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0200_TelemetryName.md)
QYL0201 | Metrics | Warning | Qyl0201InvalidMetricNameAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0201_InvalidMetricName.md)
QYL0202 | Metrics | Warning | Qyl0202HighCardinalityMetricTagAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0202_HighCardinalityMetricTag.md)
QYL0301 | Configuration | Warning | Qyl0301NonOtlpCollectorEndpointAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0301_NonOtlpCollectorEndpoint.md)
QYL0304 | Configuration | Warning | Qyl0304EnableEventSourceSupportAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0304_EnableEventSourceSupport.md)
QYL0305 | OpenTelemetry | Warning | Qyl0305LegacySemanticConventionsAccessorAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0305_LegacySemanticConventionsAccessor.md)
QYL0400 | OpenTelemetry.SemanticConventions | Warning | GenAiExecuteToolNameAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0400_GenAiExecuteToolName.md)
QYL0401 | GenAI | Warning | Qyl0401GenAiMissingRequiredAttributesAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0401_GenAiMissingRequiredAttributes.md)
QYL0402 | GenAI | Warning | Qyl0402UseTokenUsageHistogramAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0402_UseTokenUsageHistogram.md)
QYL0403 | GenAI | Warning | Qyl0012InvalidAttributeValueAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0403_InvalidAttributeValue.md)
QYL0406 | GenAI | Warning | Qyl0406DirectGenAiSdkUsageAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0406_DirectGenAiSdkUsage.md)
QYL0600 | Configuration | Warning | Qyl0600InsecureEndpointAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0600_InsecureEndpoint.md)
QYL0601 | OpenTelemetry | Warning | Qyl0601SensitiveDataInAttributeAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0601_SensitiveDataInAttribute.md)
QYL0700 | OpenTelemetry | Warning | Qyl0700MissingOtlpConfigurationAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0700_MissingOtlpConfiguration.md)
QYL0701 | OpenTelemetry | Warning | Qyl0701UncompressedExportAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0701_UncompressedExport.md)
QYL0702 | OpenTelemetry | Warning | Qyl0702BatchExportDisabledAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0702_BatchExportDisabled.md)
QYL0703 | OpenTelemetry | Info | Qyl0703ConsiderSamplingAnalyzer, [Documentation](https://github.com/ANcpLua/Qyl.Telemetry.SemanticConventions/blob/main/docs/rules/QYL0703_ConsiderSampling.md)
