using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Extracts an assembly-level package-projection marker
/// (<c>[assembly: SemanticConventionAttributesPackage("&lt;root namespace&gt;")]</c> and its
/// siblings). The target of an assembly attribute is the compilation unit; the payload is
/// the package root namespace the projection lays its files out under.
/// </summary>
internal static class PackageMarkerExtractor
{
    public static SemConvPackageMarkerModel? Extract(
        GeneratorAttributeSyntaxContext context,
        StabilityFilter filter,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetSymbol is not IAssemblySymbol)
            return null;

        var attributeData = context.Attributes.FirstOrDefault();
        if (attributeData is null || attributeData.ConstructorArguments.Length == 0)
            return null;

        if (attributeData.ConstructorArguments[0].Value is not string rootNamespace)
            return null;

        if (string.IsNullOrWhiteSpace(rootNamespace))
            return null;

        return new SemConvPackageMarkerModel(rootNamespace, filter);
    }
}
