using System.IO;
using System.Reflection;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Loads the embedded resolved-registry projection once per analyzer assembly load.
/// The JSON shape is qyl-owned (not the upstream <c>resolved-registry-v2</c> contract);
/// it is the minimum projection needed for source generation, emitted by a custom Jinja
/// template pinned to semconv v1.41.0 + Weaver v0.23.0.
/// </summary>
/// <remarks>
/// Uses a minimal hand-rolled JSON reader rather than <c>System.Text.Json</c> because
/// shipping a runtime dependency on STJ in a Roslyn analyzer is a known IDE-version
/// clash hazard (the analyzer DLL would load alongside the IDE's bundled STJ and
/// may produce binding-redirect surprises). The reader covers exactly the shape
/// of <see cref="JsonReader"/>'s grammar.
/// </remarks>
internal static class RegistryLoader
{
    private const string ResourceName = "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json";

    private static readonly Lazy<JsonObject?> _root = new(LoadRootFromEmbeddedResource);
    private static readonly Lazy<RegistryModel> _registry = new(static () => ParseRegistry(_root.Value));
    private static readonly Lazy<InstrumentRegistryModel> _instruments = new(static () => ParseInstruments(_root.Value));

    public static RegistryModel Registry => _registry.Value;

    public static InstrumentRegistryModel Instruments => _instruments.Value;

    /// <summary>
    /// The embedded registry parsed once into its JSON object root. Shared with
    /// <see cref="ActivityRegistryLoader"/> so the embedded resource is read and
    /// parsed a single time per analyzer-assembly load rather than once per loader.
    /// </summary>
    internal static JsonObject? Root => _root.Value;

    private static JsonObject? LoadRootFromEmbeddedResource()
    {
        var assembly = typeof(RegistryLoader).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        // Fail loud: `as JsonObject` would silently return null and degrade
        // every downstream model to empty, masking packaging/schema regressions
        // (wrong resource embedded, registry truncated, etc.).
        var parsed = JsonReader.Parse(text);
        if (parsed is not JsonObject root)
            throw new FormatException(
                $"Embedded resource '{ResourceName}' in {assembly.FullName} must parse as a JSON object; got {parsed.GetType().Name}.");
        return root;
    }

    internal static RegistryModel ParseRegistry(JsonObject? root)
    {
        if (root is null)
            return new RegistryModel(default, default);

        var groups = root.TryGetArray("groups") is { } groupsArr
            ? ParseGroups(groupsArr)
            : default;

        var catalog = root.TryGetArray("catalog") is { } catalogArr
            ? ParseCatalog(catalogArr)
            : default;

        return new RegistryModel(groups, catalog);
    }

    internal static InstrumentRegistryModel ParseInstruments(JsonObject? root)
    {
        if (root is null)
            return new InstrumentRegistryModel(default, default);

        var catalog = root.TryGetArray("catalog") is { } catalogArr
            ? BuildAttributeIndex(catalogArr)
            : new Dictionary<string, AttributeModel>(StringComparer.Ordinal);

        var metrics = root.TryGetArray("metrics") is { } metricsArr
            ? ParseMetrics(metricsArr)
            : default;

        var events = root.TryGetArray("events") is { } eventsArr
            ? ParseEvents(eventsArr, catalog)
            : default;

        return new InstrumentRegistryModel(metrics, events);
    }

    private static Dictionary<string, AttributeModel> BuildAttributeIndex(JsonArray catalogArr)
    {
        var byKey = new Dictionary<string, AttributeModel>(StringComparer.Ordinal);
        foreach (var attr in ParseCatalog(catalogArr))
            byKey[attr.Key] = attr;
        return byKey;
    }

    private static EquatableArray<MetricDescriptorModel> ParseMetrics(JsonArray metricsArr)
    {
        var metrics = new List<MetricDescriptorModel>(metricsArr.Items.Count);
        foreach (var item in metricsArr.Items)
        {
            if (item is not JsonObject metric) continue;

            var attributes = metric.TryGetArray("attributes") is { } attributesArr
                ? ParseSignalAttributes(attributesArr, defaultStability: RegistryParsing.ParseStability(metric.GetString("stability")))
                : default;

            metrics.Add(new MetricDescriptorModel(
                MetricName: metric.GetString("metric_name"),
                Instrument: metric.GetString("instrument"),
                Unit: metric.GetString("unit"),
                MetricRequirementLevel: RegistryParsing.ParseRequirementLevel(metric.TryGet("metric_requirement_level")),
                Brief: metric.GetString("brief"),
                Note: metric.GetString("note"),
                Stability: RegistryParsing.ParseStability(metric.GetString("stability")),
                Deprecated: RegistryParsing.ParseDeprecated(metric.TryGet("deprecated") as JsonObject),
                Attributes: attributes,
                EntityAssociations: ParseStringArray(metric.TryGetArray("entity_associations"))));
        }
        return metrics.ToEquatableArray();
    }

    private static EquatableArray<EventGroupModel> ParseEvents(
        JsonArray eventsArr,
        Dictionary<string, AttributeModel> attributeIndex)
    {
        var events = new List<EventGroupModel>(eventsArr.Items.Count);
        foreach (var item in eventsArr.Items)
        {
            if (item is not JsonObject ev) continue;

            var payload = new List<SignalAttributeModel>();
            if (ev.TryGetArray("payload") is { } payloadArr)
            {
                foreach (var value in payloadArr.Items)
                {
                    if (value is not JsonObject p) continue;

                    var key = p.GetString("key");
                    var type = p.TryGet("type") is { } typeNode
                        ? ParseType(typeNode, RegistryParsing.ParseStability(p.GetString("stability"), defaultStability: StabilityModel.Development))
                        : attributeIndex.TryGetValue(key, out var catalogAttr)
                            ? catalogAttr.Type
                            : new AttributeTypeModel.Primitive("string");
                    var brief = p.TryGet("brief") is JsonString briefStr
                        ? briefStr.Value
                        : attributeIndex.TryGetValue(key, out var briefAttr)
                            ? briefAttr.Brief
                            : string.Empty;
                    var note = p.TryGet("note") is JsonString noteStr
                        ? noteStr.Value
                        : attributeIndex.TryGetValue(key, out var noteAttr)
                            ? noteAttr.Note
                            : string.Empty;
                    payload.Add(new SignalAttributeModel(
                        Key: key,
                        Type: type,
                        RequirementLevel: RegistryParsing.ParseRequirementLevel(p.TryGet("requirement_level")),
                        Brief: brief,
                        Note: note,
                        Deprecated: RegistryParsing.ParseDeprecated(p.TryGet("deprecated") as JsonObject),
                        Examples: RegistryParsing.ParseExamples(p.TryGetArray("examples"))));
                }
            }

            events.Add(new EventGroupModel(
                EventName: ev.GetString("event_name"),
                Brief: ev.GetString("brief"),
                Note: ev.GetString("note"),
                Stability: RegistryParsing.ParseStability(ev.GetString("stability")),
                Deprecated: RegistryParsing.ParseDeprecated(ev.TryGet("deprecated") as JsonObject),
                EmissionTarget: ParseEventEmissionTarget(ev.GetString("emission_target")),
                BodyJson: ev.TryGet("body") is { } body ? RegistryParsing.ToCompactJson(body) : string.Empty,
                EntityAssociations: ParseStringArray(ev.TryGetArray("entity_associations")),
                Payload: payload.ToEquatableArray()));
        }
        return events.ToEquatableArray();
    }

    private static EquatableArray<SignalAttributeModel> ParseSignalAttributes(
        JsonArray attributesArr,
        StabilityModel defaultStability)
    {
        var attributes = new List<SignalAttributeModel>(attributesArr.Items.Count);
        foreach (var item in attributesArr.Items)
        {
            if (item is not JsonObject attr) continue;

            var stability = RegistryParsing.ParseStability(attr.GetString("stability"), defaultStability);
            attributes.Add(new SignalAttributeModel(
                Key: attr.GetString("key"),
                Type: ParseType(attr.TryGet("type"), stability),
                RequirementLevel: RegistryParsing.ParseRequirementLevel(attr.TryGet("requirement_level")),
                Brief: attr.GetString("brief"),
                Note: attr.GetString("note"),
                Deprecated: RegistryParsing.ParseDeprecated(attr.TryGet("deprecated") as JsonObject),
                Examples: RegistryParsing.ParseExamples(attr.TryGetArray("examples"))));
        }

        return attributes.ToEquatableArray();
    }

    private static EquatableArray<GroupModel> ParseGroups(JsonArray groupsArr)
    {
        var groups = new List<GroupModel>(groupsArr.Items.Count);
        foreach (var item in groupsArr.Items)
        {
            if (item is not JsonObject group) continue;

            var refs = new List<string>();
            if (group.TryGetArray("attribute_refs") is { } refsArr)
            {
                foreach (var value in refsArr.Items)
                {
                    if (value is JsonString s) refs.Add(s.Value);
                }
            }

            groups.Add(new GroupModel(
                Prefix: group.GetString("prefix"),
                AttributeRefs: refs.ToEquatableArray()));
        }
        return groups.ToEquatableArray();
    }

    private static EquatableArray<string> ParseStringArray(JsonArray? array)
    {
        if (array is null)
            return default;

        var values = new List<string>(array.Items.Count);
        foreach (var value in array.Items)
        {
            if (value is JsonString s)
                values.Add(s.Value);
        }

        return values.ToEquatableArray();
    }

    private static EquatableArray<AttributeModel> ParseCatalog(JsonArray catalogArr)
    {
        var attributes = new List<AttributeModel>(catalogArr.Items.Count);
        foreach (var item in catalogArr.Items)
        {
            if (item is not JsonObject attr) continue;

            var stability = RegistryParsing.ParseStability(attr.GetString("stability"));

            attributes.Add(new AttributeModel(
                Key: attr.GetString("key"),
                Type: ParseType(attr.TryGet("type"), stability),
                Brief: attr.GetString("brief"),
                Note: attr.GetString("note"),
                Stability: stability,
                Deprecated: RegistryParsing.ParseDeprecated(attr.TryGet("deprecated") as JsonObject),
                Examples: RegistryParsing.ParseExamples(attr.TryGetArray("examples"))));
        }
        return attributes.ToEquatableArray();
    }

    private static EventEmissionTargetModel ParseEventEmissionTarget(string value) => value switch
    {
        "log_record" or "logger" or "event" => EventEmissionTargetModel.LogRecord,
        "activity_event" => EventEmissionTargetModel.ActivityEvent,
        _ => EventEmissionTargetModel.Unspecified
    };

    private static AttributeTypeModel ParseType(
        JsonValue? value,
        StabilityModel defaultStability = StabilityModel.Development)
    {
        if (value is JsonString s)
        {
            return s.Value.StartsWithOrdinal("template[")
                ? new AttributeTypeModel.Template()
                : new AttributeTypeModel.Primitive(s.Value);
        }

        if (value is JsonObject obj && obj.TryGetArray("members") is { } membersArr)
        {
            var members = new List<EnumMemberModel>();
            foreach (var item in membersArr.Items)
            {
                if (item is not JsonObject member) continue;
                members.Add(new EnumMemberModel(
                    Id: member.GetString("id"),
                    Value: member.GetString("value"),
                    Brief: member.GetString("brief"),
                    Stability: RegistryParsing.ParseStability(member.GetString("stability"), defaultStability),
                    Deprecated: RegistryParsing.ParseDeprecated(member.TryGet("deprecated") as JsonObject)));
            }
            return new AttributeTypeModel.EnumType(members.ToEquatableArray());
        }

        return new AttributeTypeModel.Primitive("string");
    }
}
