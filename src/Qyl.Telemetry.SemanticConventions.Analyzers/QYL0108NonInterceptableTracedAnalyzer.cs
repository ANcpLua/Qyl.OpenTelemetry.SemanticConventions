
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0108: Detects [Traced] on abstract, extern, or partial definition methods that cannot be intercepted.
/// </summary>
/// <remarks>
///     <para>
///         The [Traced] attribute on a method triggers compile-time interception to create spans.
///         Certain method kinds cannot be intercepted by the source generator:
///         <list type="bullet">
///             <item><b>Abstract methods</b>: Have no implementation to intercept</item>
///             <item><b>Extern methods</b>: Implemented externally (P/Invoke)</item>
///             <item><b>Partial definitions</b>: Only the definition half, no body to wrap</item>
///         </list>
///     </para>
///     <para>
///         The [Traced] attribute will be silently ignored on these methods, which may
///         mislead developers into thinking spans are being created.
///     </para>
///     <para>
///         Example of problematic code:
///         <code>
///         public abstract class BaseService {
///             [Traced("MyApp")]
///             public abstract Task ProcessAsync();  // Cannot be intercepted!
///         }
///         </code>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0108NonInterceptableTracedAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0108.</summary>
    public const string DiagnosticId = "QYL0108";

    private const string TracedAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedAttribute";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers symbol actions to analyze methods with [Traced] for interceptability.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        NonInterceptableAttributeDetection.Register(context, TracedAttributeFullName, s_rule);
}
