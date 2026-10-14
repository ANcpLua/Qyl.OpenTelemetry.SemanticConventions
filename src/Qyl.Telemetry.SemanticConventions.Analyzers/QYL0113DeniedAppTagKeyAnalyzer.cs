namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0113: a business key (<c>qyl.app.*</c>) contains no token the collector denies.
/// </summary>
/// <remarks>
///     The collector drops every key that contains one of the registry's denied tokens (the
///     persistence policy of registry/qyl/policy.yaml, which <c>RegistryPolicy</c> carries), so
///     <c>SetAppTag("order.message", ...)</c> compiles, runs and never arrives. Only business keys
///     are checked: registry and vendor keys are the collector's own business.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0113DeniedAppTagKeyAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0113";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var invocation = (IInvocationOperation)context.Operation;
        string? key = null;
        IOperation? anchor = null;

        if (invocation.TargetMethod is { Name: "SetAppTag", ContainingType: { Name: "Spans", ContainingNamespace: { Name: "Qyl" } ns } }
            && ns.ContainingNamespace.IsGlobalNamespace
            && invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Name == "key") is { } keyArgument
            && TagSetterDetection.TryGetNonEmptyStringConstant(keyArgument.Value, out var appKey)) {
            key = SemconvTypedSurface.AppKeyPrefix + appKey;
            anchor = keyArgument;
        }
        else if (TagSetterDetection.IsTagSetterInvocation(invocation)
                 && TagSetterDetection.TryGetTagSetterKeyArgument(invocation, out var rawKeyArgument)
                 && TagSetterDetection.TryGetNonEmptyStringConstant(rawKeyArgument.Value, out var rawKey)
                 && rawKey.StartsWith(SemconvTypedSurface.AppKeyPrefix, StringComparison.Ordinal)) {
            key = rawKey;
            anchor = rawKeyArgument;
        }

        if (key is null || anchor is null) {
            return;
        }

        foreach (var token in SemconvTypedSurface.DeniedKeyTokens) {
            if (key.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, anchor.Syntax.GetLocation(), key, token));
                return;
            }
        }
    }
}
