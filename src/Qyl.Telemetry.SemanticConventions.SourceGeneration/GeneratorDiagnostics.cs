using Microsoft.CodeAnalysis;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
/// Diagnostics the generators report. The definition surfaces (metrics, spans, events,
/// entities) emit fields typed against <c>Qyl.Telemetry.SemanticConventions</c>; that
/// package is the vocabulary's one home for those types, so a consumer that does not
/// reference it gets a single actionable error instead of a wall of unresolved types.
/// </summary>
internal static class GeneratorDiagnostics
{
    /// <summary>
    /// Metadata name probed on the consuming compilation to decide whether the definition
    /// types are available. <c>MetricDefinition`1</c> stands in for the whole family, which
    /// ships together in the vocabulary package.
    /// </summary>
    public const string DefinitionTypesProbe = "Qyl.Telemetry.SemanticConventions.MetricDefinition`1";

    public static readonly DiagnosticDescriptor DefinitionTypesNotReferenced = new(
        id: "QYLSG001",
        title: "Semantic-convention definition types are not referenced",
        messageFormat: "'{0}' is marked [{1}] but the compilation does not reference Qyl.Telemetry.SemanticConventions, which defines MetricDefinition<T>, SpanDefinition<T>, EventDefinition, and EntityDefinition; add a PackageReference to Qyl.Telemetry.SemanticConventions",
        category: "SourceGeneration",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The definition generators emit fields typed as MetricDefinition<T>, SpanDefinition<T>, EventDefinition, and EntityDefinition. Those types live in the Qyl.Telemetry.SemanticConventions package; the consuming project must reference it so every assembly shares one definition type family.");
}
