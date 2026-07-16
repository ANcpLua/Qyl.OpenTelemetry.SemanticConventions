// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0400: Enforces the additional required attribute on the execute-tool span,
/// using keys and values generated from the pinned registry.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenAiExecuteToolNameAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.GenAiExecuteToolMissingToolName];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeBlock);
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        var calls = new List<TagSetterCall>();
        foreach (var blockOperation in context.OperationBlocks)
        {
            TagSetterDetection.CollectTagSetterCalls(blockOperation, calls);
        }

        if (calls.Count == 0)
        {
            return;
        }

        TagSetterCall executeToolMarker = default;
        var hasMarker = false;
        var hasToolName = false;

        foreach (var call in calls)
        {
            if (call.Key == SemconvRegistryFacts.ExecuteToolOperationKey
                && call.Value == SemconvRegistryFacts.ExecuteToolOperationValue)
            {
                executeToolMarker = call;
                hasMarker = true;
            }
            else if (call.Key == SemconvRegistryFacts.ExecuteToolRequiredAttribute)
            {
                hasToolName = true;
            }
        }

        if (hasMarker && !hasToolName)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenAiExecuteToolMissingToolName,
                executeToolMarker.KeyLocation,
                SemconvRegistryFacts.ExecuteToolOperationKey,
                SemconvRegistryFacts.ExecuteToolOperationValue,
                SemconvRegistryFacts.ExecuteToolRequiredAttribute));
        }
    }
}
