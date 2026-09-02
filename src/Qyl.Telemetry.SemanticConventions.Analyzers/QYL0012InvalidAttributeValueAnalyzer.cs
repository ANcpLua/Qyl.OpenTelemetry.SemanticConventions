namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0012: Detects a known semantic-convention enum value written with casing that
/// contradicts the complete pinned registry. Unknown values remain valid because many
/// semantic-convention enums explicitly permit documented system-specific extensions.
/// GenAI span-discriminator attributes report QYL0403 instead, preserving that rule's
/// GenAI category and severity; the guard chain is otherwise identical.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0012InvalidAttributeValueAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0012";

    private const string GenAiDiagnosticId = "QYL0403";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    private static readonly DiagnosticDescriptor s_genAiRule = CreateRule(
        GenAiDiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule, s_genAiRule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!TagSetterDetection.IsTagSetterInvocation(invocation)
            || !TagSetterDetection.TryGetTagSetterKeyArgument(invocation, out var keyArgument)
            || !TagSetterDetection.TryGetTagSetterValueArgument(invocation, out var valueArgument)
            || !TagSetterDetection.TryGetNonEmptyStringConstant(keyArgument.Value, out var attributeName)
            || !TagSetterDetection.TryGetStringConstant(valueArgument.Value, out var value)
            || !SemconvRegistryFacts.TryGetCanonicalEnumValue(attributeName, value, out var canonical)
            || string.Equals(value, canonical, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            SemconvRegistryFacts.IsGenAiSpanDiscriminator(attributeName)
                ? Diagnostic.Create(s_genAiRule, valueArgument.Syntax.GetLocation(), value, canonical)
                : Diagnostic.Create(
                    s_rule,
                    valueArgument.Syntax.GetLocation(),
                    attributeName,
                    value,
                    $"exact registry spelling '{canonical}'"));
    }
}
