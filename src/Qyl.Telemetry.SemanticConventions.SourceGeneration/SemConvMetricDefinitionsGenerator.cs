using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for first-class OpenTelemetry
///   semantic-convention metric definitions. Triggered by
///   <c>[SemanticConventionMetricDefinitions("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingMetricDefinitions("&lt;prefix&gt;")]</c>
///   (incubating superset). Emits <c>public static readonly MetricDefinition&lt;TInstrument&gt;</c>
///   fields — the name is one property, the instrument is a marker type (compile-time
///   safety), and unit, stability, entity associations, and structured deprecation
///   travel with the object. The definition types themselves ship in the
///   <c>Qyl.Telemetry.SemanticConventions</c> package; a compilation without that
///   reference gets <c>QYLSG001</c> at the marker.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvMetricDefinitionsGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionMetricDefinitionsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetricDefinitionsAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionMetricDefinitionsAttribute",
            "SemanticConventionIncubatingMetricDefinitionsAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => MetricDefinitionsEmitter.Generate(marker, RegistryLoader.Instruments),
            requiresDefinitionTypes: true);
}
