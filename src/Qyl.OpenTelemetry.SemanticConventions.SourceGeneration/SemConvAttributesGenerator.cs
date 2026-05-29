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

    // Kept for backwards-compat with any callers that still reference the
    // single-marker constant; equivalent to <see cref="StableAttributeFullName"/>.
    internal const string AttributeFullName = StableAttributeFullName;

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("SemanticConventionAttributesAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionAttributesAttribute"));
            ctx.AddSource("SemanticConventionIncubatingAttributesAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionIncubatingAttributesAttribute"));
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
            var file = AttributesEmitter.Generate(marker, RegistryLoader.Registry);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });

        context.RegisterSourceOutput(incubatingMarkers, static (spc, marker) =>
        {
            var file = AttributesEmitter.Generate(marker, RegistryLoader.Registry);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });
    }
}
