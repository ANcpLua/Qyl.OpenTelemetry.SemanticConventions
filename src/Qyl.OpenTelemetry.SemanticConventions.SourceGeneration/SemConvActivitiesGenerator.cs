using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

/// <summary>
///   Roslyn incremental source generator for OpenTelemetry semantic-convention
///   Activity tag-setter extensions. Triggered by
///   <c>[SemanticConventionActivities("&lt;prefix&gt;")]</c> (stable surface) or
///   <c>[SemanticConventionIncubatingActivities("&lt;prefix&gt;")]</c> (incubating
///   superset) on a user-declared <c>static partial class</c>. Emits typed
///   <c>this Activity</c> extension methods that wrap <c>SetTag</c> with the
///   semconv key + value-type guarantees pinned in the embedded resolved-registry.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class SemConvActivitiesGenerator : IIncrementalGenerator
{
    internal const string StableAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionActivitiesAttribute";

    internal const string IncubatingAttributeFullName =
        "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.SemanticConventionIncubatingActivitiesAttribute";

    internal const string AttributeFullName = StableAttributeFullName;

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("SemanticConventionActivitiesAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionActivitiesAttribute"));
            ctx.AddSource("SemanticConventionIncubatingActivitiesAttribute.g.cs",
                MarkerAttributeSource.For("SemanticConventionIncubatingActivitiesAttribute"));
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
            var file = ActivityExtensionsEmitter.Generate(marker, ActivityRegistryLoader.Registry);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });

        context.RegisterSourceOutput(incubatingMarkers, static (spc, marker) =>
        {
            var file = ActivityExtensionsEmitter.Generate(marker, ActivityRegistryLoader.Registry);
            if (!file.IsEmpty)
                spc.AddSource(file.Name, file.Text);
        });
    }
}
