using System.Text.RegularExpressions;

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0201: Detects metric descriptor names that are malformed or unknown to the generated catalog.
/// </summary>
/// <remarks>
///     <para>
///         Metric names declared on Counter/Histogram descriptor attributes must follow OpenTelemetry
///         naming conventions (dot-separated namespaces, snake_case words) and be members of the
///         generated registry catalog (<see cref="SemconvRegistryFacts"/>), so the collector recognizes
///         every metric qyl emits (qyl architecture, loop 1). qyl-owned metrics enter the catalog through
///         qyl-registry.json, never through a hardcoded list here.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0201InvalidMetricNameAnalyzer : AlAnalyzer {
    private const string CounterAttributeFullName = "Qyl.Instrumentation.Instrumentation.CounterAttribute";
    private const string HistogramAttributeFullName = "Qyl.Instrumentation.Instrumentation.HistogramAttribute";

    // Pattern: lowercase letters, numbers, dots, and underscores only
    // Should have at least one dot (namespace separator)
    private static readonly Regex s_validNamePattern = new(
        @"^[a-z][a-z0-9_.]*\.[a-z][a-z0-9_.]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The diagnostic identifier for QYL0201.</summary>
    private const string DiagnosticId = "QYL0201";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Metrics,
        DiagnosticSeverities.RequiredFix);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers symbol actions to analyze methods with metric attributes.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

    private static void AnalyzeMethod(SymbolAnalysisContext context) {
        var method = (IMethodSymbol)context.Symbol;

        var counterType = context.Compilation.GetTypeByMetadataName(CounterAttributeFullName);
        var histogramType = context.Compilation.GetTypeByMetadataName(HistogramAttributeFullName);

        foreach (var attribute in method.GetAttributes()) {
            if (!attribute.AttributeClass.IsEqualTo(counterType) &&
                !attribute.AttributeClass.IsEqualTo(histogramType)) {
                continue;
            }

            if (attribute.ConstructorArguments.Length is 0 ||
                attribute.ConstructorArguments[0].Value is not string metricName ||
                string.IsNullOrWhiteSpace(metricName) ||
                (s_validNamePattern.IsMatch(metricName) &&
                 SemconvRegistryFacts.IsKnownMetricName(metricName))) {
                continue;
            }

            var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken)
                .GetLocation() ?? method.Locations.FirstOrDefault();

            if (location is not null) {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, location, metricName));
            }
        }
    }
}
