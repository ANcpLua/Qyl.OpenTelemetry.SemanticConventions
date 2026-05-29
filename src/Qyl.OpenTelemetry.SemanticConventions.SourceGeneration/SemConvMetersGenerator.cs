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
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("SemanticConventionMetersAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionMetersAttribute"));
            ctx.AddSource("SemanticConventionIncubatingMetersAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionIncubatingMetersAttribute"));
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
            var file = MetersEmitter.Generate(marker, RegistryLoader.Instruments);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });

        context.RegisterSourceOutput(incubatingMarkers, static (spc, marker) =>
        {
            var file = MetersEmitter.Generate(marker, RegistryLoader.Instruments);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });
    }
}
