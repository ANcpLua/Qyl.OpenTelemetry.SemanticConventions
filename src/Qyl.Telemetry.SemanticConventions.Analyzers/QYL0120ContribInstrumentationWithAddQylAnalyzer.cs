namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0120: no <c>OpenTelemetry.Instrumentation.*</c> tracer extension in a compilation that calls <c>AddQyl</c>.
/// </summary>
/// <remarks>
///     <c>AddQyl()</c> already subscribes the HTTP server, HttpClient, Npgsql, Kafka and GenAI spans;
///     a contrib instrumentation package records the same operations a second time. The rule
///     reports every <c>TracerProviderBuilder</c> extension from an assembly named
///     <c>OpenTelemetry.Instrumentation.*</c>, also inside a tracing callback, once the
///     compilation is known to call <c>AddQyl</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0120ContribInstrumentationWithAddQylAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0120";
    private const string InstrumentationAssemblyPrefix = "OpenTelemetry.Instrumentation.";

    private static readonly DiagnosticDescriptor Rule = CreateCompilationEndRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context) {
        if (context.Compilation.GetTypeByMetadataName("OpenTelemetry.Trace.TracerProviderBuilder") is not { } builderType) {
            return;
        }

        var callsAddQyl = 0;
        var candidates = new ConcurrentQueue<Diagnostic>();

        context.RegisterOperationAction(
            ctx => {
                var method = ((IInvocationOperation)ctx.Operation).TargetMethod;
                if (method is { Name: "AddQyl", ContainingNamespace: { Name: "Qyl" } ns } && ns.ContainingNamespace.IsGlobalNamespace) {
                    Interlocked.Exchange(ref callsAddQyl, 1);
                    return;
                }

                if (method is { IsExtensionMethod: true, ContainingAssembly.Name: { } assembly }
                    && assembly.StartsWith(InstrumentationAssemblyPrefix, StringComparison.Ordinal)
                    && method.Parameters.FirstOrDefault()?.Type is { } receiver
                    && (receiver.IsEqualTo(builderType) || receiver.InheritsFrom(builderType))) {
                    candidates.Enqueue(Diagnostic.Create(Rule, ctx.Operation.Syntax.GetLocation(), method.Name, assembly));
                }
            },
            OperationKind.Invocation);

        context.RegisterCompilationEndAction(ctx => {
            if (Volatile.Read(ref callsAddQyl) == 0) {
                return;
            }

            foreach (var diagnostic in candidates.OrderBy(static d => d.Location.SourceSpan.Start)) {
                ctx.ReportDiagnostic(diagnostic);
            }
        });
    }
}
