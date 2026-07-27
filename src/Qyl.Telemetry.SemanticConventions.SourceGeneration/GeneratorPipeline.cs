using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Shared incremental pipeline for the four semantic-convention generators. Each
///   generator publishes its stable/incubating marker attributes, discovers annotated
///   partial classes for both, and emits through its surface-specific emitter; only
///   the marker names and the emit function differ per surface.
/// </summary>
internal static class GeneratorPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        string stableMarkerName,
        string incubatingMarkerName,
        string stableAttributeFullName,
        string incubatingAttributeFullName,
        Func<SemConvMarkerModel, FileWithName> emit)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource($"{stableMarkerName}.g.cs", MarkerAttributeSource.For(stableMarkerName));
            ctx.AddSource($"{incubatingMarkerName}.g.cs", MarkerAttributeSource.For(incubatingMarkerName));
        });

        var stableMarkers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                stableAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.StableOnly, ct))
            .WhereNotNull();

        var incubatingMarkers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                incubatingAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.AllStabilities, ct))
            .WhereNotNull();

        context.RegisterSourceOutput(stableMarkers, (spc, marker) => Emit(spc, marker, emit));
        context.RegisterSourceOutput(incubatingMarkers, (spc, marker) => Emit(spc, marker, emit));
    }

    private static void Emit(
        SourceProductionContext spc,
        SemConvMarkerModel marker,
        Func<SemConvMarkerModel, FileWithName> emit)
    {
        var file = emit(marker);
        if (!file.IsEmpty)
            spc.AddSource(file.Name, file.Text);
    }
}
