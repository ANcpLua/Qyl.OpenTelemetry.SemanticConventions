using System.Collections.Concurrent;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0304: EventSource/EventCounter instrumentation is trimmed to a no-op under Native AOT
///     unless <c>EventSourceSupport</c> is enabled.
/// </summary>
/// <remarks>
///     <para>
///         Under Native AOT with <c>EventSourceSupport</c> unset, the runtime trims EventSource/EventPipe:
///         any in-process <c>EventSource</c>, <c>EventListener</c>, or <c>EventCounter</c> — including the
///         <c>OpenTelemetry.Instrumentation.EventCounters</c> bridge — silently stops emitting at runtime,
///         with no build error. This rule fires only when such instrumentation is actually present in the
///         compilation together with <c>PublishAot=true</c> and <c>EventSourceSupport</c> unset, so it does
///         not nag the common OTLP-push app.
///     </para>
///     <para>
///         OpenTelemetry's OTLP export path (<c>ActivitySource</c> + <c>Meter</c>/<c>MeterListener</c>) does
///         not depend on EventSource and is unaffected; enabling <c>EventSourceSupport</c> increases Native
///         AOT output size, so it should be set only when EventPipe-based instrumentation is relied upon.
///     </para>
///     <para>
///         The out-of-process case (attaching <c>dotnet-trace</c>/<c>dotnet-counters</c> at runtime) is not
///         detectable at compile time and is intentionally out of scope.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0304EnableEventSourceSupportAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0304.</summary>
    private const string DiagnosticId = "QYL0304";

    private const string PublishAotProperty = "build_property.PublishAot";
    private const string EventSourceSupportProperty = "build_property.EventSourceSupport";
    private const string EventSourceMetadataName = "System.Diagnostics.Tracing.EventSource";
    private const string EventListenerMetadataName = "System.Diagnostics.Tracing.EventListener";
    private const string EventCountersAssemblyName = "OpenTelemetry.Instrumentation.EventCounters";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Configuration,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers a compilation-start action that gates on MSBuild properties before any work.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context) {
        var globalOptions = context.Options.AnalyzerConfigOptionsProvider.GlobalOptions;

        // Gate 1: only Native AOT publishes are affected.
        if (!globalOptions.TryGetValue(PublishAotProperty, out var publishAot)
            || !string.Equals(publishAot, "true", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        // Gate 2: if EventSourceSupport is already on, nothing is trimmed.
        if (globalOptions.TryGetValue(EventSourceSupportProperty, out var eventSourceSupport)
            && string.Equals(eventSourceSupport, "true", StringComparison.OrdinalIgnoreCase)) {
            return;
        }

        var eventSourceType = context.Compilation.GetTypeByMetadataName(EventSourceMetadataName);
        var eventListenerType = context.Compilation.GetTypeByMetadataName(EventListenerMetadataName);

        // Signal: the EventCounters→OTel bridge package (consumes EventCounters via EventListener internally).
        var referencesEventCounters = false;
        foreach (var assembly in context.Compilation.ReferencedAssemblyNames) {
            if (assembly.Name.EqualsOrdinal(EventCountersAssemblyName)) {
                referencesEventCounters = true;
                break;
            }
        }

        // Signal: in-process EventSource / EventListener subclasses defined in this compilation.
        var hits = new ConcurrentBag<(Location Location, string Name)>();
        context.RegisterSymbolAction(symbolContext => {
            var type = (INamedTypeSymbol)symbolContext.Symbol;
            if (InheritsFrom(type, eventSourceType) || InheritsFrom(type, eventListenerType)) {
                hits.Add((type.Locations.FirstOrDefault() ?? Location.None, type.Name));
            }
        }, SymbolKind.NamedType);

        context.RegisterCompilationEndAction(endContext => {
            if (!hits.IsEmpty) {
                foreach (var (location, name) in hits) {
                    endContext.ReportDiagnostic(Diagnostic.Create(s_rule, location, name));
                }
            }
            else if (referencesEventCounters) {
                endContext.ReportDiagnostic(Diagnostic.Create(s_rule, Location.None, EventCountersAssemblyName));
            }
        });
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol? baseType) {
        if (baseType is null) {
            return false;
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) {
                return true;
            }
        }

        return false;
    }
}
