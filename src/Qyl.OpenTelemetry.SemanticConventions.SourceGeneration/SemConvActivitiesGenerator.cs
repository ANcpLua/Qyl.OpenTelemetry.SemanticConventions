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

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        GeneratorPipeline.Register(
            context,
            "SemanticConventionActivitiesAttribute",
            "SemanticConventionIncubatingActivitiesAttribute",
            StableAttributeFullName,
            IncubatingAttributeFullName,
            static marker => ActivityExtensionsEmitter.Generate(marker, ActivityRegistryLoader.Registry));
}
