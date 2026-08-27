using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Shared incremental pipeline for the semantic-convention generators. Each generator
///   publishes its stable/incubating marker attributes, discovers annotated partial
///   classes for both, and emits through its surface-specific emitter; only the marker
///   names, the emit function, and whether the surface needs the vocabulary package's
///   definition types differ per surface.
/// </summary>
/// <remarks>
///   A surface registered with <c>requiresDefinitionTypes</c> emits members typed against
///   <c>Qyl.Telemetry.SemanticConventions</c>; a compilation that does not reference that
///   package gets <c>QYLSG001</c> at the marker instead of generated source.
/// </remarks>
internal static class GeneratorPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        string stableMarkerName,
        string incubatingMarkerName,
        string stableAttributeFullName,
        string incubatingAttributeFullName,
        Func<SemConvMarkerModel, FileWithName> emit,
        bool requiresDefinitionTypes = false)
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
                (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.StableOnly, requiresDefinitionTypes, ct))
            .WhereNotNull();

        var incubatingMarkers = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                incubatingAttributeFullName,
                static (node, _) => node is ClassDeclarationSyntax,
                (ctx, ct) => MarkerExtractor.Extract(ctx, StabilityFilter.AllStabilities, requiresDefinitionTypes, ct))
            .WhereNotNull();

        var stableDisplayName = DisplayName(stableMarkerName);
        var incubatingDisplayName = DisplayName(incubatingMarkerName);

        context.RegisterSourceOutput(stableMarkers, (spc, marker) => Emit(spc, marker, stableDisplayName, emit));
        context.RegisterSourceOutput(incubatingMarkers, (spc, marker) => Emit(spc, marker, incubatingDisplayName, emit));
    }

    private static void Emit(
        SourceProductionContext spc,
        SemConvMarkerModel marker,
        string markerDisplayName,
        Func<SemConvMarkerModel, FileWithName> emit)
    {
        if (marker.DefinitionTypesMissingAt is { } location)
        {
            spc.ReportDiagnostic(Diagnostic.Create(
                GeneratorDiagnostics.DefinitionTypesNotReferenced,
                location.ToLocation(),
                marker.ClassName,
                markerDisplayName));
            return;
        }

        var file = emit(marker);
        if (!file.IsEmpty)
            spc.AddSource(file.Name, file.Text);
    }

    /// <summary>The marker as written at the use site: <c>SemanticConventionMetricDefinitions</c>, not <c>...Attribute</c>.</summary>
    private static string DisplayName(string markerAttributeName) =>
        markerAttributeName.EndsWithOrdinal("Attribute")
            ? markerAttributeName.Substring(0, markerAttributeName.Length - "Attribute".Length)
            : markerAttributeName;
}
