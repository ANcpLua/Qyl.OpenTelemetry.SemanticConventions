
namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0304: Enable EventSourceSupport for AOT with telemetry.
/// </summary>
/// <remarks>
///     <para>
///         Native AOT trims EventSource/EventPipe infrastructure by default. This only
///         matters if you consume EventPipe-based diagnostics (dotnet-trace, dotnet-counters)
///         or bridge EventSource/EventCounters into telemetry. OpenTelemetry's OTLP export
///         path (ActivitySource + Meter/MeterListener) does <b>not</b> depend on EventSource,
///         and enabling <c>&lt;EventSourceSupport&gt;true&lt;/EventSourceSupport&gt;</c> increases
///         Native AOT output size — so set it only when you actually rely on EventPipe.
///     </para>
///     <para>
///         This analyzer checks MSBuild properties exposed via <c>CompilerVisibleProperty</c> to detect
///         when <c>PublishAot</c> is true but <c>EventSourceSupport</c> is not explicitly enabled. It
///         cannot tell whether an EventPipe consumer is actually present, so it fires on any AOT project
///         without the switch; treat it as an informational reminder, not a mandate.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0304EnableEventSourceSupportAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0304.</summary>
    private const string DiagnosticId = "QYL0304";

    private const string PublishAotProperty = "build_property.PublishAot";
    private const string EventSourceSupportProperty = "build_property.EventSourceSupport";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Configuration,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers a compilation action to check MSBuild properties.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterCompilationAction(AnalyzeCompilation);

    private static void AnalyzeCompilation(CompilationAnalysisContext context) {
        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

        if (!globalOptions.TryGetValue(PublishAotProperty, out var publishAot)
            || !string.Equals(publishAot, "true", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        if (globalOptions.TryGetValue(EventSourceSupportProperty, out var eventSourceSupport)
            && string.Equals(eventSourceSupport, "true", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, Location.None));
    }
}
