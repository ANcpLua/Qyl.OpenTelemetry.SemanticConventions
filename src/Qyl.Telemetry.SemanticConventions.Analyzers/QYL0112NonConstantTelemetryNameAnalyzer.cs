namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0112: a <c>Spans.Work</c> name is a compile-time constant.
/// </summary>
/// <remarks>
///     A composed name (<c>$"write {what}"</c>) produces an unbounded set of operation identities.
///     Put varying values into native <c>Activity.SetTag</c> attributes instead.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0112NonConstantTelemetryNameAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0112";

    private static readonly DiagnosticDescriptor Rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.ContainingType is not { Name: "Spans" } type
            || type.ContainingNamespace is not { Name: "Qyl" } ns
            || !ns.ContainingNamespace.IsGlobalNamespace) {
            return;
        }

        var (parameter, what) = method.Name switch {
            "Work" => ("name", "Spans.Work name"),
            _ => (null, null),
        };

        if (parameter is null
            || invocation.Arguments.FirstOrDefault(candidate => candidate.Parameter?.Name == parameter) is not { } argument
            || argument.Value.ConstantValue.HasValue) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), what, argument.Value.Syntax.ToString()));
    }
}
