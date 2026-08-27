using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for first-class OpenTelemetry
///   semantic-convention entity definitions. Triggered by
///   <c>[SemanticConventionEntityDefinitions("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingEntityDefinitions("&lt;prefix&gt;")]</c>
///   (incubating superset). Emits <c>public static readonly EntityDefinition</c> fields
///   carrying the entity's describing/identifying attributes. The definition types ship
///   in the <c>Qyl.Telemetry.SemanticConventions</c> package; a compilation without that
///   reference gets <c>QYLSG001</c> at the marker.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvEntityDefinitionsGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionEntityDefinitionsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingEntityDefinitionsAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionEntityDefinitionsAttribute",
            "SemanticConventionIncubatingEntityDefinitionsAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => EntityDefinitionsEmitter.Generate(marker, RegistryLoader.Signals),
            requiresDefinitionTypes: true);
}
