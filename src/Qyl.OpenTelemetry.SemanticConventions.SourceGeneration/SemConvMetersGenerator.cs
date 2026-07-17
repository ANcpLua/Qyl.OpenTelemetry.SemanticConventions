using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for OpenTelemetry semantic-convention
///   <c>Meter</c> instrument factories. Triggered by
///   <c>[SemanticConventionMeters("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingMeters("&lt;prefix&gt;")]</c> (incubating
///   superset). Emits <c>public static Histogram&lt;double&gt; Create&lt;Name&gt;(
///   this Meter meter)</c> style factories that wire the canonical name + unit
///   from the resolved-registry pin.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvMetersGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionMetersAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetersAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionMetersAttribute",
            "SemanticConventionIncubatingMetersAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => MetersEmitter.Generate(marker, RegistryLoader.Instruments));
}
