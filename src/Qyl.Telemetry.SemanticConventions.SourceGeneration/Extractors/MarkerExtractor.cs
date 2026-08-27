using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Shared marker extraction for every semantic-convention surface. All marker attributes
/// take the same <c>(string prefix)</c> constructor and project to the same
/// <see cref="SemConvMarkerModel"/>, so a single extractor serves them all — the owning
/// generator supplies the <see cref="StabilityFilter"/> that distinguishes the stable from
/// the incubating pipeline, and says whether the surface needs the vocabulary package's
/// definition types.
/// </summary>
internal static class MarkerExtractor
{
    public static SemConvMarkerModel? Extract(
        GeneratorAttributeSyntaxContext context,
        StabilityFilter filter,
        bool requiresDefinitionTypes,
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

        // The location is captured only when the diagnostic will be reported, so the
        // healthy model stays position-free and edits above the marker do not re-emit.
        LocationInfo? missingAt = null;
        if (requiresDefinitionTypes &&
            context.SemanticModel.Compilation.GetTypeByMetadataName(GeneratorDiagnostics.DefinitionTypesProbe) is null)
        {
            missingAt = context.TargetNode is ClassDeclarationSyntax declaration
                ? LocationInfo.From(declaration.Identifier)
                : LocationInfo.From(context.TargetNode);
        }

        return new SemConvMarkerModel(ns, typeSymbol.Name, prefix, filter, missingAt);
    }
}
