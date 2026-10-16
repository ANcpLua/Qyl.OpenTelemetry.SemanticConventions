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

    private static readonly DiagnosticDescriptor Rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    private static readonly DiagnosticDescriptor GenAiRule = CreateRule(
        GenAiDiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, GenAiRule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!TagSetterDetection.TryGetTagWrite(invocation, out var attributeName, out var valueArgument)
            || !TagSetterDetection.TryGetValueConstant(valueArgument.Value, out var value)
            || !SemconvRegistryFacts.TryGetCanonicalEnumValue(attributeName, value, out var canonical)
            || string.Equals(value, canonical, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            SemconvRegistryFacts.IsGenAiSpanDiscriminator(attributeName)
                ? Diagnostic.Create(GenAiRule, valueArgument.Syntax.GetLocation(), value, canonical)
                : Diagnostic.Create(
                    Rule,
                    valueArgument.Syntax.GetLocation(),
                    attributeName,
                    value,
                    $"exact registry spelling '{canonical}'"));
    }
}
