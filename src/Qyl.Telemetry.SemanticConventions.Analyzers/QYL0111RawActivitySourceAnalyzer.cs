namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0111: application code starts spans only through the generated starters.
/// </summary>
/// <remarks>
///     A hand-made <c>ActivitySource</c> is the unregistered-source failure (its spans are dropped
///     unless something subscribes it) and its <c>StartActivity</c> calls carry no registry kind,
///     name or attributes. <c>Qyl.Spans</c> starts every span on the registry-owned application
///     scope that <c>AddQyl</c> subscribes. Instrumentation libraries that own their sources opt
///     out with the MSBuild property <c>OtelSemConvInstrumentationLibrary=true</c>; generated code
///     is not analyzed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0111RawActivitySourceAnalyzer : AlAnalyzer {
    private const string DiagnosticId = "QYL0111";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context) {
        if (SemconvAnalyzerOptions.IsInstrumentationLibrary(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions)
            || context.Compilation.GetTypeByMetadataName("System.Diagnostics.ActivitySource") is not { } activitySource) {
            return;
        }

        context.RegisterOperationAction(
            ctx => {
                var creation = (IObjectCreationOperation)ctx.Operation;
                if (SymbolEqualityComparer.Default.Equals(creation.Type, activitySource)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(s_rule, creation.Syntax.GetLocation(), "new ActivitySource(...)"));
                }
            },
            OperationKind.ObjectCreation);

        context.RegisterOperationAction(
            ctx => {
                var invocation = (IInvocationOperation)ctx.Operation;
                if (invocation.TargetMethod.Name is "StartActivity" or "CreateActivity"
                    && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, activitySource)) {
                    ctx.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.Syntax.GetLocation(), "ActivitySource." + invocation.TargetMethod.Name));
                }
            },
            OperationKind.Invocation);
    }
}
