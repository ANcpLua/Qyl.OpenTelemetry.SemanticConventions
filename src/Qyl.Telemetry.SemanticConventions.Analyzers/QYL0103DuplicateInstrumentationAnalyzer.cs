using MsOperationExtensions = Microsoft.CodeAnalysis.Operations.OperationExtensions;

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0103: Detects duplicate instrumentation - methods with both auto-instrumentation and manual spans.
/// </summary>
/// <remarks>
///     <para>
///         When a method is instrumented by both auto-instrumentation (e.g., [Traced] attribute or
///         interceptor) AND manual Activity.StartActivity spans, it creates duplicate nested spans that:
///         <list type="bullet">
///             <item>Increase telemetry volume unnecessarily</item>
///             <item>Cause confusion in trace visualization</item>
///             <item>May lead to incorrect span duration calculations</item>
///         </list>
///     </para>
///     <para>
///         Example of problematic code:
///         <code>
///         [Traced("MyApp")]
///         public void ProcessOrder()
///         {
///             using var activity = ActivitySource.StartActivity("ProcessOrder"); // Duplicate!
///             // ...
///         }
///         </code>
///     </para>
///     <para>
///         Solution: Choose one instrumentation approach per method.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0103DuplicateInstrumentationAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0103.</summary>
    private const string DiagnosticId = "QYL0103";

    private const string TracedAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedAttribute";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers actions to analyze methods with [Traced] attribute.</summary>
    protected override void InitializeCore(AnalysisContext context) {
        context.RegisterCompilationStartAction(compilationContext => {
            if (compilationContext.Compilation.GetTypeByMetadataName(TracedAttributeFullName) is not { } tracedAttributeType) {
                return;
            }

            compilationContext.RegisterOperationBlockAction(ctx =>
                AnalyzeOperationBlock(ctx, tracedAttributeType));
        });
    }

    private static void AnalyzeOperationBlock(OperationBlockAnalysisContext context, INamedTypeSymbol tracedAttributeType) {
        if (context.OwningSymbol is not IMethodSymbol method) {
            return;
        }

        // [Traced] on the method itself, or inherited from the containing type
        if (!method.HasAttribute(tracedAttributeType)
            && (method.ContainingType is null || !method.ContainingType.HasAttribute(tracedAttributeType))) {
            return;
        }

        foreach (var operationBlock in context.OperationBlocks) {
            if (ContainsStartActivityCall(operationBlock)) {
                var location = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken).GetLocation()
                               ?? Location.None;

                context.ReportDiagnostic(Diagnostic.Create(s_rule, location, method.Name));
                return;
            }
        }
    }

    private static bool ContainsStartActivityCall(IOperation operation) {
        foreach (var descendant in MsOperationExtensions.DescendantsAndSelf(operation)) {
            if (descendant is IInvocationOperation invocation &&
                IsStartActivityMethod(invocation.TargetMethod)) {
                return true;
            }
        }

        return false;
    }

    private static bool IsStartActivityMethod(IMethodSymbol method) =>
        method is { Name: "StartActivity", ContainingType.Name: "ActivitySource" };
}
