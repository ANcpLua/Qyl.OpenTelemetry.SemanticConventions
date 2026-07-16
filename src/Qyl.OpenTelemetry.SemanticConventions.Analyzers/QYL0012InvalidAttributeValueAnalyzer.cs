namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0012: Detects a known semantic-convention enum value written with casing that
/// contradicts the complete pinned registry. Unknown values remain valid because many
/// semantic-convention enums explicitly permit documented system-specific extensions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0012InvalidAttributeValueAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0012";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

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
            || SemconvRegistryFacts.IsGenAiSpanDiscriminator(attributeName)
            || !TagSetterDetection.TryGetStringConstant(valueArgument.Value, out var value)
            || !SemconvRegistryFacts.TryGetCanonicalEnumValue(attributeName, value, out var canonical)
            || string.Equals(value, canonical, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                valueArgument.Syntax.GetLocation(),
                attributeName,
                value,
                $"exact registry spelling '{canonical}'"));
    }
}
