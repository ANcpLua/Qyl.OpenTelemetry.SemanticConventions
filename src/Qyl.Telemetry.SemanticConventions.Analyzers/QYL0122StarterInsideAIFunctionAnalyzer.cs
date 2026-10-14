namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0122: no span starter inside a delegate passed to <c>AIFunctionFactory.Create</c>.
/// </summary>
/// <remarks>
///     <c>FunctionInvokingChatClient</c> records the <c>execute_tool</c> span of every
///     <c>AIFunction</c> it invokes, and the registry says not to record two spans for one tool
///     call. Inside the function, set business keys on <c>Activity.Current</c> with
///     <c>SetAppTag</c> instead of starting a span.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0122StarterInsideAIFunctionAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0122";
    private const string AIFunctionFactory = "Microsoft.Extensions.AI.AIFunctionFactory";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var invocation = (IInvocationOperation)context.Operation;
        if (!TagSetterDetection.TryGetStarter(invocation, out var starter)) {
            return;
        }

        for (var current = invocation.Parent; current is not null; current = current.Parent) {
            if (current is IAnonymousFunctionOperation && IsAIFunctionDelegate(current)) {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.Syntax.GetLocation(), starter.Substring("Qyl.".Length)));
                return;
            }
        }
    }

    private static bool IsAIFunctionDelegate(IOperation function) {
        var current = function.Parent;
        while (current is IDelegateCreationOperation or IConversionOperation) {
            current = current.Parent;
        }

        return current is IArgumentOperation { Parent: IInvocationOperation create }
               && create.TargetMethod.Name == "Create"
               && create.TargetMethod.ContainingType?.ToDisplayString() == AIFunctionFactory;
    }
}
