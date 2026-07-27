using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for OpenTelemetry semantic-convention
///   metric constants and descriptors. Triggered by
///   <c>[SemanticConventionMetrics("&lt;prefix&gt;")]</c> (stable) or
///   <c>[SemanticConventionIncubatingMetrics("&lt;prefix&gt;")]</c> (incubating
///   superset). Emits the canonical metric-name constant, descriptor class with
///   instrument kind + unit + requirement-level metadata, and attribute-name
///   constants used for tagging measurements.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvMetricsGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionMetricsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetricsAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionMetricsAttribute",
            "SemanticConventionIncubatingMetricsAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => MetricsEmitter.Generate(marker, RegistryLoader.Instruments));
}
