using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for OpenTelemetry semantic-convention
///   attribute-key constants. Two projections share the registry:
///   <list type="bullet">
///     <item><c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c> (stable surface) or
///     <c>[SemanticConventionIncubatingAttributes("&lt;prefix&gt;")]</c> (incubating superset)
///     on a user-declared <c>static partial class</c> emits <c>public const string Attribute*</c>
///     definitions and enum-value classes matching the contrib-shape contract (see
///     ByteIdentitySnapshotTests).</item>
///     <item><c>[assembly: SemanticConventionAttributesPackage("&lt;package root&gt;")]</c> or
///     <c>[assembly: SemanticConventionIncubatingAttributesPackage("&lt;package root&gt;")]</c>
///     emits the whole tier in the compiled-package layout: one
///     <c>{package root}.Attributes.{Root}.{Root}Attributes</c> class per registry root, plus
///     <c>{package root}.SchemaUrl</c> for the stable tier. This is how the
///     <c>Qyl.Telemetry.SemanticConventions</c> and <c>.Incubating</c> packages are built.</item>
///   </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvAttributesGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingAttributesAttribute";

    internal const string StablePackageAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionAttributesPackageAttribute";

    internal const string IncubatingPackageAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingAttributesPackageAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        GeneratorPipeline.Register(
            context,
            "SemanticConventionAttributesAttribute",
            "SemanticConventionIncubatingAttributesAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => AttributesEmitter.Generate(marker, RegistryLoader.Registry));

        GeneratorPipeline.RegisterPackageProjection(
            context,
            "SemanticConventionAttributesPackageAttribute",
            StablePackageAttributeFullName,
            StabilityFilter.StableOnly,
            static marker => PackageAttributesEmitter.Generate(marker, RegistryLoader.Registry));

        GeneratorPipeline.RegisterPackageProjection(
            context,
            "SemanticConventionIncubatingAttributesPackageAttribute",
            IncubatingPackageAttributeFullName,
            StabilityFilter.AllStabilities,
            static marker => PackageAttributesEmitter.Generate(marker, RegistryLoader.Registry));
    }
}
