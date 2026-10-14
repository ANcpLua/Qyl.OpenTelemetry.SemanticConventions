namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0121: <c>Custom</c> carries a value the registry does not list, never a member's value.
/// </summary>
/// <remarks>
///     <c>DbSystemName.Custom("postgresql")</c> works, but it hides the registry member from every
///     reader and from rename tooling; write <c>DbSystemName.Postgresql</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0121CustomValueEqualsMemberAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0121";

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
        var method = invocation.TargetMethod;
        if (method is not { Name: "Custom", IsStatic: true }
            || invocation.Arguments.Length != 1
            || !TagSetterDetection.TryGetValueSetKey(method.ContainingType, out var key)
            || invocation.Arguments[0].Value.UnwrapAllConversions().ConstantValue is not { HasValue: true, Value: { } constant }
            || Convert.ToString(constant, CultureInfo.InvariantCulture) is not { } value) {
            return;
        }

        foreach (var member in SemconvTypedSurface.ValueSetMembers[key]) {
            if (string.Equals(member.Value, value, StringComparison.Ordinal)) {
                context.ReportDiagnostic(Diagnostic.Create(
                    s_rule,
                    invocation.Syntax.GetLocation(),
                    value,
                    method.ContainingType.Name + "." + member.Key));
                return;
            }
        }
    }
}
