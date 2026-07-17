using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for OpenTelemetry semantic-convention
///   attribute-key constants. Triggered by
///   <c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c> (stable surface) or
///   <c>[SemanticConventionIncubatingAttributes("&lt;prefix&gt;")]</c> (incubating
///   superset) on a user-declared <c>static partial class</c>. Emits
///   <c>public const string Attribute*</c> definitions and enum-value classes
///   matching the contrib-shape contract (see ByteIdentitySnapshotTests).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvAttributesGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingAttributesAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionAttributesAttribute",
            "SemanticConventionIncubatingAttributesAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => AttributesEmitter.Generate(marker, RegistryLoader.Registry));
}
