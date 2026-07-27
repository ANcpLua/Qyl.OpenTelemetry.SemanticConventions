using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

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
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionMetersAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetersAttribute";

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
