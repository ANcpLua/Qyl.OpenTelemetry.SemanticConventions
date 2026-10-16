namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>Suggests registry-defined GenAI token histogram names at the .NET Meter API.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0402UseTokenUsageHistogramAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0402";
    private static readonly DiagnosticDescriptor Rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeHistogram, OperationKind.Invocation);

    private static void AnalyzeHistogram(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "CreateHistogram"
            || invocation.TargetMethod.ContainingType.ToDisplayString() != "System.Diagnostics.Metrics.Meter")
        {
            return;
        }

        var argument = invocation.Arguments.FirstOrDefault(static argument => argument.Parameter?.Name == "name");
        if (argument is null
            || !argument.Value.TryGetConstantValue<string>(out var name)
            || !name.StartsWith("gen_ai.", StringComparison.Ordinal)
            || !name.ContainsIgnoreCase("token")
            || SemconvRegistryFacts.IsKnownMetricName(name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Value.Syntax.GetLocation(), name,
            string.Join("', '", SemconvRegistryFacts.GenAiTokenHistogramNames)));
    }
}
