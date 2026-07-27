namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Resolved registry as embedded into the analyzer assembly. One per shipped semconv version.
/// </summary>
internal readonly record struct RegistryModel(
    EquatableArray<GroupModel> Groups,
    EquatableArray<AttributeModel> Catalog);

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
    EquatableArray<string> Examples);

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
/// </summary>
internal readonly record struct SemConvMarkerModel(
    string ContainingNamespace,
    string ClassName,
    string Prefix,
    Extractors.StabilityFilter Filter);
