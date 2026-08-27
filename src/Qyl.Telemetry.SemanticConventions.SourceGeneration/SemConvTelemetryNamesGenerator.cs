using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for the qyl-owned telemetry names (scope names
///   and event names carried by the merged registry). Triggered by the assembly-level
///   <c>[assembly: SemanticConventionTelemetryNamesPackage("&lt;package root&gt;")]</c>;
///   emits <c>{package root}.Names.QylTelemetryNames</c> in the compiled-package layout.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvTelemetryNamesGenerator : IIncrementalGenerator
{
    internal const string PackageAttributeFullName =
        "Qyl.Telemetry.SemanticConventions.SourceGeneration.SemanticConventionTelemetryNamesPackageAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.RegisterPackageProjection(
            context,
            "SemanticConventionTelemetryNamesPackageAttribute",
            PackageAttributeFullName,
            StabilityFilter.AllStabilities,
            static marker => TelemetryNamesEmitter.Generate(marker, RegistryLoader.Registry));
}
