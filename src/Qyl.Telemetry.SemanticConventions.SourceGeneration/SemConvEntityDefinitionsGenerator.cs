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
///   carrying the entity's describing/identifying attributes. Shared runtime support types
///   are emitted by <see cref="SemConvMetricDefinitionsGenerator"/>.
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
            static marker => EntityDefinitionsEmitter.Generate(marker, RegistryLoader.Signals));
}
