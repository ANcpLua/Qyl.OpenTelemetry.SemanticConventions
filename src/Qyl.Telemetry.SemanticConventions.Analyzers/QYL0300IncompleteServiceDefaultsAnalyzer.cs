
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0300: Detects incomplete ServiceDefaults configuration.
/// </summary>
/// <remarks>
///     <para>
///         Complete ServiceDefaults configuration should include:
///         <list type="bullet">
///             <item>Tracing configuration (AddTracing/WithTracing)</item>
///             <item>Metrics configuration (AddMetrics/WithMetrics)</item>
///             <item>Logging configuration (AddLogging/WithLogging)</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0300IncompleteServiceDefaultsAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0300.</summary>
    private const string DiagnosticId = "QYL0300";

    private static readonly string[] s_tracingMethods = ["AddOpenTelemetry", "WithTracing", "AddTracing"];
    private static readonly string[] s_metricsMethods = ["AddOpenTelemetry", "WithMetrics", "AddMetrics"];

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Configuration,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers syntax tree actions to analyze ServiceDefaults configuration.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var methodName = invocation.GetMethodName();
        if (methodName is not ("ConfigureOpenTelemetry" or "AddServiceDefaults" or "AddOpenTelemetry")) {
            return;
        }

        if (invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } containingMethod) {
            return;
        }

        var allInvocations = new HashSet<string>();
        foreach (var inv in containingMethod.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
            var name = inv.GetMethodName();
            if (name is not null) {
                allInvocations.Add(name);
            }
        }

        var hasTracing = s_tracingMethods.Any(allInvocations.Contains);
        var hasMetrics = s_metricsMethods.Any(allInvocations.Contains);

        if (!hasTracing) {
            context.ReportDiagnostic(Diagnostic.Create(
                s_rule,
                invocation.GetLocation(),
                "tracing",
                "WithTracing() or AddTracing()"));
        }

        if (!hasMetrics) {
            context.ReportDiagnostic(Diagnostic.Create(
                s_rule,
                invocation.GetLocation(),
                "metrics",
                "WithMetrics() or AddMetrics()"));
        }

        // Note: Logging is optional, so we don't report it as missing
    }
}
