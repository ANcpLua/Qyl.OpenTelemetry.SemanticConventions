namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0403: Detects a known GenAI span discriminator value whose casing differs
/// from the pinned registry. System-specific operation names remain allowed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0403InvalidGenAiOperationNameAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0403";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!TagSetterDetection.IsTagSetterInvocation(invocation)
            || !TagSetterDetection.TryGetTagSetterKeyArgument(invocation, out var keyArgument)
            || !TagSetterDetection.TryGetTagSetterValueArgument(invocation, out var valueArgument)
            || !TagSetterDetection.TryGetNonEmptyStringConstant(keyArgument.Value, out var attributeName)
            || !SemconvRegistryFacts.IsGenAiSpanDiscriminator(attributeName)
            || !TagSetterDetection.TryGetStringConstant(valueArgument.Value, out var value)
            || !SemconvRegistryFacts.TryGetCanonicalEnumValue(attributeName, value, out var canonical)
            || string.Equals(value, canonical, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(s_rule, valueArgument.Syntax.GetLocation(), value, canonical));
    }
}
