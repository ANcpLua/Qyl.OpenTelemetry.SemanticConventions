namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0112: a <c>Spans.Work</c> name and a <c>SetAppTag</c> key are compile-time constants.
/// </summary>
/// <remarks>
///     The span name is the operation's identity in the collector's failure signatures and the
///     business key is a column; a composed one (<c>$"write {what}"</c>) turns each into an
///     unbounded set. Put the varying part into a <c>SetAppTag</c> value instead.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0112NonConstantTelemetryNameAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0112";

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
        if (method.ContainingType is not { Name: "Spans" } type
            || type.ContainingNamespace is not { Name: "Qyl" } ns
            || !ns.ContainingNamespace.IsGlobalNamespace) {
            return;
        }

        var (parameter, what) = method.Name switch {
            "Work" => ("name", "Spans.Work name"),
            "SetAppTag" => ("key", "SetAppTag key"),
            _ => (null, null),
        };

        if (parameter is null
            || invocation.Arguments.FirstOrDefault(candidate => candidate.Parameter?.Name == parameter) is not { } argument
            || argument.Value.ConstantValue.HasValue) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, argument.Syntax.GetLocation(), what, argument.Value.Syntax.ToString()));
    }
}
