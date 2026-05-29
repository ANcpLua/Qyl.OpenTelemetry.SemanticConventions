using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

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
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionMetricsAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingMetricsAttribute";

    internal const string AttributeFullName = StableAttributeFullName;

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("SemanticConventionMetricsAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionMetricsAttribute"));
            ctx.AddSource("SemanticConventionIncubatingMetricsAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionIncubatingMetricsAttribute"));
        });

        var stableMarkers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                StableAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.StableOnly, ct))
            .WhereNotNull();

        var incubatingMarkers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                IncubatingAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.AllStabilities, ct))
            .WhereNotNull();

        context.RegisterSourceOutput(stableMarkers, static (spc, marker) =>
        {
            var file = MetricsEmitter.Generate(marker, RegistryLoader.Instruments);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });

        context.RegisterSourceOutput(incubatingMarkers, static (spc, marker) =>
        {
            var file = MetricsEmitter.Generate(marker, RegistryLoader.Instruments);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });
    }
}
