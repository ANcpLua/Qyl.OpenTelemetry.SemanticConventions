using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for first-class OpenTelemetry
///   semantic-convention event definitions. Triggered by
///   <c>[SemanticConventionEventDefinitions("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingEventDefinitions("&lt;prefix&gt;")]</c>
///   (incubating superset). Emits <c>public static readonly EventDefinition</c> fields;
///   stability, structured deprecation, and attribute references travel with the object.
///   The definition types ship in the <c>Qyl.Telemetry.SemanticConventions</c> package;
///   a compilation without that reference gets <c>QYLSG001</c> at the marker.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvEventDefinitionsGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionEventDefinitionsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingEventDefinitionsAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionEventDefinitionsAttribute",
            "SemanticConventionIncubatingEventDefinitionsAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => EventDefinitionsEmitter.Generate(marker, RegistryLoader.Signals),
            requiresDefinitionTypes: true);
}
