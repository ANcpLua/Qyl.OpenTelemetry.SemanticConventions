// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0001: A method that sets <c>gen_ai.operation.name = "execute_tool"</c> must
/// also set <c>gen_ai.tool.name</c>. v1.41.0 made the tool-name attribute required
/// because the canonical span name format is <c>execute_tool {gen_ai.tool.name}</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenAiExecuteToolNameAnalyzer : DiagnosticAnalyzer
{
    private const string OperationNameKey = "gen_ai.operation.name";
    private const string ExecuteToolValue = "execute_tool";
    private const string ToolNameKey = "gen_ai.tool.name";

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
            if (call is { Key: OperationNameKey, Value: ExecuteToolValue })
            {
                executeToolMarker = call;
                hasMarker = true;
            }
            else if (call.Key == ToolNameKey)
            {
                hasToolName = true;
            }
        }

        if (hasMarker && !hasToolName)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenAiExecuteToolMissingToolName,
                executeToolMarker.KeyLocation));
        }
    }
}
