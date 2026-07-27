
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0405: Detects [AgentTraced] on abstract, extern, or partial definition methods that cannot be intercepted.
/// </summary>
/// <remarks>
///     The [AgentTraced] attribute on a method triggers compile-time interception to create agent spans.
///     Certain method kinds cannot be intercepted by the source generator:
///     <list type="bullet">
///         <item><b>Abstract methods</b>: Have no implementation to intercept</item>
///         <item><b>Extern methods</b>: Implemented externally (P/Invoke)</item>
///         <item><b>Partial definitions</b>: Only the definition half, no body to wrap</item>
///     </list>
///     The [AgentTraced] attribute will be silently ignored on these methods, which may
///     mislead developers into thinking agent spans are being created.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0405NonInterceptableAgentTracedAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0405.</summary>
    public const string DiagnosticId = "QYL0405";

    private const string AgentTracedAttributeFullName = "Qyl.Instrumentation.Instrumentation.AgentTracedAttribute";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers symbol actions to analyze methods with [AgentTraced] for interceptability.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        NonInterceptableAttributeDetection.Register(context, AgentTracedAttributeFullName, s_rule);
}
