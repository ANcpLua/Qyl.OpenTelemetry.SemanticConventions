namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

/// <summary>
/// Metric projection of the embedded Weaver-derived registry. Metrics and
/// meter factories intentionally share <see cref="MetricDescriptorModel"/> so both
/// surfaces preserve the same name, instrument, unit, attributes, examples, and
/// entity-association facts.
/// </summary>
internal readonly record struct InstrumentRegistryModel(
    EquatableArray<MetricDescriptorModel> Metrics);

/// <summary>
/// A semconv metric group (a registry entry with <c>type == "metric"</c>).
/// </summary>
internal readonly record struct MetricDescriptorModel(
    string MetricName,
    string Instrument,
    string Unit,
    RequirementLevelModel MetricRequirementLevel,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<SignalAttributeModel> Attributes,
    EquatableArray<string> EntityAssociations);

/// <summary>
/// One signal-specific attribute reference, preserving the upstream requirement
/// level and any local brief/note/examples override supplied on the signal.
/// </summary>
internal readonly record struct SignalAttributeModel(
    string Key,
    AttributeTypeModel Type,
    RequirementLevelModel RequirementLevel,
    string Brief,
    string Note,
    DeprecatedModel? Deprecated,
    EquatableArray<string> Examples);

internal readonly record struct RequirementLevelModel(
    RequirementLevelKind Kind,
    string Condition);

internal enum RequirementLevelKind
{
    Unspecified,
    Required,
    Recommended,
    OptIn,
    ConditionallyRequired
}

/// <summary>
/// Span/event projection of the resolved registry, consumed by the first-class
/// span- and event-definition emitters.
/// </summary>
internal readonly record struct SignalRegistryModel(
    EquatableArray<SpanDescriptorModel> Spans,
    EquatableArray<EventDescriptorModel> Events,
    EquatableArray<EntityDescriptorModel> Entities);

/// <summary>A semconv entity (a registry entry with <c>type == "entity"</c>).</summary>
internal readonly record struct EntityDescriptorModel(
    string Name,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<SignalAttributeModel> Attributes);

/// <summary>A semconv span group (a registry group with <c>type == "span"</c>).</summary>
internal readonly record struct SpanDescriptorModel(
    string Id,
    string SpanKind,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<SignalAttributeModel> Attributes);

/// <summary>A semconv event (a registry entry with <c>type == "event"</c>).</summary>
internal readonly record struct EventDescriptorModel(
    string EventName,
    string Brief,
    string Note,
    StabilityModel Stability,
    DeprecatedModel? Deprecated,
    EquatableArray<SignalAttributeModel> Attributes);
