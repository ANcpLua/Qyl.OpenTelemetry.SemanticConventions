using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for first-class OpenTelemetry
///   semantic-convention span definitions. Triggered by
///   <c>[SemanticConventionSpanDefinitions("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingSpanDefinitions("&lt;prefix&gt;")]</c>
///   (incubating superset). Emits <c>public static readonly SpanDefinition&lt;TKind&gt;</c>
///   fields; the span kind is a marker type and stability, structured deprecation, and
///   attribute references travel with the object. The definition types ship in the
///   <c>Qyl.Telemetry.SemanticConventions</c> package; a compilation without that
///   reference gets <c>QYLSG001</c> at the marker.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvSpanDefinitionsGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionSpanDefinitionsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingSpanDefinitionsAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionSpanDefinitionsAttribute",
            "SemanticConventionIncubatingSpanDefinitionsAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => SpanDefinitionsEmitter.Generate(marker, RegistryLoader.Signals),
            requiresDefinitionTypes: true);
}
