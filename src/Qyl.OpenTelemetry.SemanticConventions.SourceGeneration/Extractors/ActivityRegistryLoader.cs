using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Loads the attribute-shaped projection of the embedded resolved registry for
/// PR-E (<c>System.Diagnostics.Activity</c> typed setters). Returns the catalog
/// flat — the activity emitter filters by marker prefix. Shares the single parsed
/// JSON root with <see cref="RegistryLoader"/>, so the embedded resource is parsed
/// once per analyzer-assembly load and projected three ways
/// (<see cref="RegistryModel"/>, <see cref="InstrumentRegistryModel"/>, and this).
/// </summary>
internal static class ActivityRegistryLoader
{
    private static readonly Lazy<ActivityRegistryModel> _registry = new(static () => ParseRegistry(RegistryLoader.Root));

    public static ActivityRegistryModel Registry => _registry.Value;

    internal static ActivityRegistryModel ParseRegistry(JsonValue? root)
    {
        // Fail loud: silently returning an empty registry on shape regressions
        // means the generator emits nothing with no diagnostic and consumers
        // think their markers "just don't work." A FormatException surfaces at
        // analyzer-load time with a clear cause.
        if (root is not JsonObject obj)
            throw new FormatException(
                $"Embedded activity registry root must be a JSON object; got {root?.GetType().Name ?? "null"}.");

        if (obj.TryGetArray("catalog") is not { } catalogArr)
            throw new FormatException(
                "Embedded activity registry is missing the required 'catalog' array.");

        var contextsByKey = obj.TryGetArray("groups") is { } groupsArr
            ? BuildContextsByAttribute(groupsArr)
            : new Dictionary<string, List<ActivityAttributeContextModel>>(StringComparer.Ordinal);

        var attributes = new List<ActivityAttributeModel>();
        foreach (var item in catalogArr.Items)
        {
            if (item is not JsonObject attr) continue;

            var key = attr.GetString("key");
            var stability = RegistryParsing.ParseStability(attr.GetString("stability"));
            var typeValue = attr.TryGet("type");
            var (parameterType, isTemplate, isEnum, enumMembers) = ResolveSetterShape(typeValue, stability);

            attributes.Add(new ActivityAttributeModel(
                Key: key,
                CSharpParameterType: parameterType,
                IsTemplate: isTemplate,
                IsEnum: isEnum,
                EnumMembers: enumMembers,
                Brief: attr.GetString("brief"),
                Note: attr.GetString("note"),
                Stability: stability,
                Deprecated: RegistryParsing.ParseDeprecated(attr.TryGet("deprecated") as JsonObject),
                Examples: RegistryParsing.ParseExamples(attr.TryGetArray("examples")),
                Contexts: contextsByKey.TryGetValue(key, out var contexts)
                    ? contexts.ToEquatableArray()
                    : default));
        }

        return new ActivityRegistryModel(attributes.ToEquatableArray());
    }

    private static Dictionary<string, List<ActivityAttributeContextModel>> BuildContextsByAttribute(JsonArray groupsArr)
    {
        var contextsByKey = new Dictionary<string, List<ActivityAttributeContextModel>>(StringComparer.Ordinal);
        foreach (var item in groupsArr.Items)
        {
            if (item is not JsonObject group) continue;

            var groupId = group.GetString("id");
            var groupType = group.GetString("type");
            var prefix = group.GetString("prefix");

            if (group.TryGetArray("attributes") is { } attributesArr)
            {
                foreach (var value in attributesArr.Items)
                {
                    if (value is not JsonObject attr) continue;
                    AddContext(
                        contextsByKey,
                        attr.GetString("key"),
                        new ActivityAttributeContextModel(
                            GroupId: groupId,
                            GroupType: groupType,
                            Prefix: prefix,
                            RequirementLevel: RegistryParsing.ParseRequirementLevel(attr.TryGet("requirement_level"))));
                }
                continue;
            }

            if (group.TryGetArray("attribute_refs") is not { } refsArr)
                continue;

            foreach (var value in refsArr.Items)
            {
                if (value is not JsonString attrRef) continue;
                AddContext(
                    contextsByKey,
                    attrRef.Value,
                    new ActivityAttributeContextModel(
                        GroupId: groupId,
                        GroupType: groupType,
                        Prefix: prefix,
                        RequirementLevel: new RequirementLevelModel(RequirementLevelKind.Unspecified, string.Empty)));
            }
        }

        return contextsByKey;
    }

    private static void AddContext(
        Dictionary<string, List<ActivityAttributeContextModel>> contextsByKey,
        string key,
        ActivityAttributeContextModel context)
    {
        if (!contextsByKey.TryGetValue(key, out var contexts))
        {
            contexts = new List<ActivityAttributeContextModel>();
            contextsByKey.Add(key, contexts);
        }

        contexts.Add(context);
    }

    private static (string ParameterType, bool IsTemplate, bool IsEnum, EquatableArray<EnumMemberModel> Members) ResolveSetterShape(
        JsonValue? typeValue,
        StabilityModel defaultStability)
    {
        if (typeValue is JsonString s)
        {
            if (s.Value.StartsWith("template[", StringComparison.Ordinal))
            {
                var inner = ExtractTemplateInner(s.Value);
                return (MapPrimitive(inner), true, false, default);
            }
            return (MapPrimitive(s.Value), false, false, default);
        }

        if (typeValue is JsonObject obj && obj.TryGetArray("members") is { } membersArr)
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
            return ("string", false, true, members.ToEquatableArray());
        }

        return ("string", false, false, default);
    }

    private static string MapPrimitive(string primitive) => primitive switch
    {
        "string" => "string",
        "int" => "long",
        "double" => "double",
        "boolean" => "bool",
        "string[]" => "string[]",
        "int[]" => "long[]",
        "double[]" => "double[]",
        "boolean[]" => "bool[]",
        _ => "string"
    };

    private static string ExtractTemplateInner(string raw)
    {
        // raw is e.g. "template[string]" or "template[string[]]"
        const string prefix = "template[";
        if (!raw.StartsWith(prefix, StringComparison.Ordinal) || !raw.EndsWith("]", StringComparison.Ordinal))
            return "string";
        return raw.Substring(prefix.Length, raw.Length - prefix.Length - 1);
    }
}
