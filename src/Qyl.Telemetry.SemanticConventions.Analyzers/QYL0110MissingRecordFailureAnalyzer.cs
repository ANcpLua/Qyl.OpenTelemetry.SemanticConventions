namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0110: a <c>catch</c> that throws inside a span starter's scope calls <c>RecordFailure</c>.
/// </summary>
/// <remarks>
///     <para>
///         A span whose operation fails ends with status Unset, no <c>error.type</c> and no
///         exception event unless the catch records it, so the collector sees a success with an
///         empty failure message. <c>span.RecordFailure(exception)</c> sets all three.
///     </para>
///     <para>
///         A starter's scope is a <c>using</c> statement or declaration, or a local, initialized by
///         a <c>Qyl.Spans</c>/<c>Qyl.IncubatingSpans</c> starter before the <c>try</c>. A catch that
///         handles the exception without throwing is not a failure of the span.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0110MissingRecordFailureAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0110";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeCatch, OperationKind.CatchClause);

    private static void AnalyzeCatch(OperationAnalysisContext context) {
        var catchClause = (ICatchClauseOperation)context.Operation;
        if (!Throws(catchClause.Handler)
            || RecordsFailure(catchClause.Handler)
            || EnclosingStarter(catchClause) is not { } starter) {
            return;
        }

        var location = catchClause.Syntax is CatchClauseSyntax syntax
            ? syntax.CatchKeyword.GetLocation()
            : catchClause.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(s_rule, location, starter));
    }

    private static bool Throws(IOperation handler) =>
        OwnOperations(handler).Any(static operation => operation is IThrowOperation);

    private static bool RecordsFailure(IOperation handler) =>
        OwnOperations(handler).Any(static operation =>
            operation is IInvocationOperation { TargetMethod: { Name: "RecordFailure", ContainingType: { Name: "Spans", ContainingNamespace: { Name: "Qyl" } ns } } }
            && ns.ContainingNamespace.IsGlobalNamespace);

    /// <summary>The operations of <paramref name="root"/> outside nested lambdas and local functions.</summary>
    private static IEnumerable<IOperation> OwnOperations(IOperation root) {
        var pending = new Stack<IOperation>();
        pending.Push(root);
        while (pending.Count > 0) {
            var operation = pending.Pop();
            yield return operation;
            foreach (var child in operation.ChildOperations) {
                if (child is not (IAnonymousFunctionOperation or ILocalFunctionOperation)) {
                    pending.Push(child);
                }
            }
        }
    }

    /// <summary>The starter whose span the catch runs inside, as <c>Spans.Work</c>, or null.</summary>
    private static string? EnclosingStarter(IOperation operation) {
        var child = operation;
        for (var current = operation.Parent; current is not null; child = current, current = current.Parent) {
            switch (current) {
                case IUsingOperation @using when StarterIn(@using.Resources) is { } scoped:
                    return scoped;

                case IBlockOperation block:
                    foreach (var statement in block.Operations) {
                        if (ReferenceEquals(statement, child)) {
                            break;
                        }

                        if (statement is IUsingDeclarationOperation or IVariableDeclarationGroupOperation
                            && StarterIn(statement) is { } declared) {
                            return declared;
                        }
                    }

                    break;

                case IMethodBodyOperation or IAnonymousFunctionOperation or ILocalFunctionOperation:
                    return null;
            }
        }

        return null;
    }

    private static string? StarterIn(IOperation operation) {
        foreach (var descendant in operation.DescendantsAndSelf()) {
            if (descendant is IInvocationOperation invocation && TagSetterDetection.TryGetStarter(invocation, out var starter)) {
                return starter.Substring("Qyl.".Length);
            }
        }

        return null;
    }
}
