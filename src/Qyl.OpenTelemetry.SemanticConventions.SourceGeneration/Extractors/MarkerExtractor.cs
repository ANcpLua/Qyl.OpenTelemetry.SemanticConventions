using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Shared marker extraction for every semantic-convention surface. All five marker
/// attributes (attributes, metrics, events, meters, activities) take the same
/// <c>(string prefix)</c> constructor and project to the same <see cref="SemConvMarkerModel"/>,
/// so a single extractor serves them all — the owning generator supplies the
/// <see cref="StabilityFilter"/> that distinguishes the stable from the incubating pipeline.
/// </summary>
internal static class MarkerExtractor
{
    public static SemConvMarkerModel? Extract(
        GeneratorAttributeSyntaxContext context,
        StabilityFilter filter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
            return null;

        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null || attributeData.ConstructorArguments.Length == 0)
            return null;

        if (attributeData.ConstructorArguments[0].Value is not string prefix)
            return null;

        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        return new SemConvMarkerModel(ns, typeSymbol.Name, prefix, filter);
    }
}
