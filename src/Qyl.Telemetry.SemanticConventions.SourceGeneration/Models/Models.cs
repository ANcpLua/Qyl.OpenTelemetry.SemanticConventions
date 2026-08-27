namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Resolved registry as embedded into the analyzer assembly. One per shipped semconv version:
/// the merged core + GenAI + qyl projection, so <see cref="Catalog"/> rows carry their
/// <see cref="SourceModel"/> and the qyl-owned scope and event names ride along.
/// </summary>
internal readonly record struct RegistryModel(
    EquatableArray<GroupModel> Groups,
    EquatableArray<AttributeModel> Catalog,
    RegistryPinModel Pin,
    EquatableArray<string> ScopeNames,
    EquatableArray<string> EventNames);

/// <summary>The pinned schema facts of the projection: version, schema URL, and the core registry commit.</summary>
internal readonly record struct RegistryPinModel(
    string SchemaVersion,
    string SchemaUrl,
    string CoreCommit);

/// <summary>
/// Provenance of one registry row: which source registry it came from (<c>core</c>,
/// <c>genai</c>, or <c>qyl</c>) and, for upstream sources, the schema URL and commit that
/// pinned it. The package projection cites these in its generated file headers.
/// </summary>
internal readonly record struct SourceModel(
    string Registry,
    string SchemaUrl,
    string Commit);

/// <summary>
/// A semconv group projected for the attributes surface: a set of attributes sharing a
/// prefix (e.g. "disk", "http.client"). <see cref="AttributeRefs"/> stores attribute keys
/// (e.g. "disk.io.direction") that the emitter resolves against
/// <see cref="RegistryModel.Catalog"/>. Only the two fields the attributes emitter consumes
/// are projected; the metric/event/span facets of an upstream group are carried by
/// <see cref="InstrumentRegistryModel"/> instead.
/// </summary>
internal readonly record struct GroupModel(
    string Prefix,
    EquatableArray<string> AttributeRefs);

/// <summary>
/// One semconv attribute definition. Keys are dotted (e.g. "disk.io.direction"); the emitter
/// projects keys to C# identifiers via PascalCase on the dotted form.
/// </summary>
internal readonly record struct AttributeModel(
    string Key,
    AttributeTypeModel Type,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<string> Examples,
    SourceModel Source);

/// <summary>
/// Type of an attribute. Distinguishes primitives, template-typed attributes
/// (e.g. <c>http.request.header.&lt;key&gt;</c>), and enum-valued attributes.
/// </summary>
internal abstract record AttributeTypeModel
{
    public sealed record Primitive(string Name) : AttributeTypeModel;
    public sealed record Template : AttributeTypeModel;
    public sealed record EnumType(EquatableArray<EnumMemberModel> Members) : AttributeTypeModel;
}

internal readonly record struct EnumMemberModel(
    string Id,
    string Value,
    string Brief,
    StabilityModel Stability,
    DeprecatedModel? Deprecated);

internal enum StabilityModel
{
    Stable,
    Development,
    Deprecated,
    Alpha,
    Beta,
    ReleaseCandidate
}

internal abstract record DeprecatedModel
{
    public sealed record Renamed(string RenamedTo) : DeprecatedModel;
    public sealed record Obsoleted : DeprecatedModel;
    public sealed record Uncategorized(string Note) : DeprecatedModel;
}

/// <summary>
/// Extracted state from a single semantic-convention marker application — the
/// stable surface (e.g. <c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c>) or its
/// incubating counterpart (e.g. <c>[SemanticConventionIncubatingAttributes("&lt;prefix&gt;")]</c>).
/// Every marker surface (attributes, metrics, events, meters, activities) carries the
/// same four facts, so they share this one model: the user's partial class, the prefix
/// it requested, and which stability projection the surface emits
/// (<see cref="Extractors.StabilityFilter"/>). The generator that owns the pipeline picks
/// the emitter and registry; the marker payload itself is surface-agnostic.
/// <see cref="DefinitionTypesMissingAt"/> is set only for a definition surface whose
/// compilation lacks the vocabulary package; the pipeline then reports <c>QYLSG001</c>
/// there instead of emitting.
/// </summary>
internal readonly record struct SemConvMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter,
    LocationInfo? DefinitionTypesMissingAt);

/// <summary>
/// Extracted state from an assembly-level package-projection marker
/// (<c>[assembly: SemanticConventionAttributesPackage("&lt;package root namespace&gt;")]</c>
/// and its incubating and telemetry-names siblings). The projection has no prefix: it emits
/// the whole registry tier in the compiled-package layout under the given root namespace.
/// </summary>
internal readonly record struct SemConvPackageMarkerModel(
    string RootNamespace,
    Extractors.StabilityFilter Filter);
