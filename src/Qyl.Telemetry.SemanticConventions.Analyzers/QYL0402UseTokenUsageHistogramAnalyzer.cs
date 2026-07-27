namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0402: Detects GenAI token-related histogram names that are absent from the
/// complete metric inventory in the pinned registry.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0402UseTokenUsageHistogramAnalyzer : AlAnalyzer
{
    private const string HistogramAttributeFullName = "Qyl.Instrumentation.Instrumentation.HistogramAttribute";
    private const string DiagnosticId = "QYL0402";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

    private static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var method = (IMethodSymbol)context.Symbol;
        if (context.Compilation.GetTypeByMetadataName(HistogramAttributeFullName) is not { } histogramType)
        {
            return;
        }

        foreach (var attribute in method.GetAttributes())
        {
            if (!attribute.AttributeClass.IsEqualTo(histogramType)
                || attribute.ConstructorArguments.Length == 0
                || attribute.ConstructorArguments[0].Value is not string metricName
                || !IsCandidate(metricName)
                || SemconvRegistryFacts.IsKnownGenAiMetric(metricName))
            {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                ?? method.Locations.FirstOrDefault();
            if (location is not null)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        s_rule,
                        location,
                        metricName,
                        SemconvRegistryFacts.GenAiTokenUsageMetricName));
            }
        }
    }

    private static bool IsCandidate(string metricName) =>
        metricName.StartsWith("gen_ai.", StringComparison.Ordinal)
        && metricName.ContainsIgnoreCase("token");
}
