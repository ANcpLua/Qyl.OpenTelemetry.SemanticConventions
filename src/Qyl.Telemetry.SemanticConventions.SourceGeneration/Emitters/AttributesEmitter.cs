using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits the source file completing a partial class marked with
/// <c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c>.
///
/// Member shape (constant naming, enum-value classes, XML doc comment layout) is
/// byte-identical to contrib's <c>OpenTelemetry.SemanticConventions.SourceGeneration</c>
/// output for the matching semconv version. The outer class declaration is a
/// partial because the marker pattern attaches generated members to a user-authored
/// class; contrib emits its own class declaration. Byte-identity is checked on the
/// member region.
/// </summary>
internal static class AttributesEmitter
{
    public static FileWithName Generate(SemConvMarkerModel marker, RegistryModel registry)
    {
        var group = FindGroupForPrefix(registry, marker.Prefix);
        var attributes = ResolveAttributes(registry, marker.Prefix, marker.Filter, group);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, attributes, marker.Filter);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static GroupModel? FindGroupForPrefix(RegistryModel registry, string prefix)
    {
        foreach (var group in registry.Groups)
        {
            if (group.Prefix.EqualsOrdinal(prefix))
                return group;
        }
        return null;
    }

    private static List<AttributeModel> ResolveAttributes(
        RegistryModel registry,
        string prefix,
        StabilityFilter filter,
        GroupModel? group)
    {
        if (group is { } g && !g.AttributeRefs.IsEmpty)
        {
            var byKey = new Dictionary<string, AttributeModel>(StringComparer.Ordinal);
            foreach (var attr in registry.Catalog)
                byKey[attr.Key] = attr;

            var resolved = new List<AttributeModel>();
            foreach (var key in g.AttributeRefs)
            {
                if (byKey.TryGetValue(key, out var match) &&
                    StabilityFiltering.IsIncludedOrDeprecated(match.Stability, match.Deprecated, filter))
                {
                    resolved.Add(match);
                }
            }
            return resolved;
        }

        var fallback = new List<AttributeModel>();
        var dotted = prefix + ".";
        foreach (var attr in registry.Catalog)
        {
            if (attr.Key.EqualsOrdinal(prefix) ||
                attr.Key.StartsWithOrdinal(dotted))
            {
                if (!StabilityFiltering.IsIncludedOrDeprecated(attr.Stability, attr.Deprecated, filter))
                    continue;

                fallback.Add(attr);
            }
        }
        fallback.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        return fallback;
    }

    private static void WriteClass(
        StringBuilder builder,
        string className,
        List<AttributeModel> attributes,
        StabilityFilter filter)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Constants for semantic attribute names outlined by the OpenTelemetry specifications.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var attr in attributes)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteAttributeConstant(builder, attr);
        }

        foreach (var attr in attributes)
        {
            if (attr.Type is not AttributeTypeModel.EnumType enumType) continue;
            builder.AppendLine();
            WriteEnumValueClass(builder, attr, enumType, filter);
        }

        builder.AppendLine("}");
    }

    private static void WriteAttributeConstant(StringBuilder builder, AttributeModel attr)
    {
        SourceWriter.WriteSummaryComment(builder, attr.Brief, indent: 4);
        if (!string.IsNullOrEmpty(attr.Note))
            SourceWriter.WriteRemarksComment(builder, attr.Note, indent: 4);
        SourceWriter.WriteExamplesComment(builder, attr.Examples, indent: 4);
        SourceWriter.WriteObsolete(builder, attr.Deprecated, indent: 4);

        var memberName = AttributeMemberName(attr.Key, attr.Type);
        builder.Append("    public const string ").Append(memberName)
               .Append(" = \"").Append(attr.Key).AppendLine("\";");
    }

    private static void WriteEnumValueClass(
        StringBuilder builder,
        AttributeModel attr,
        AttributeTypeModel.EnumType enumType,
        StabilityFilter filter)
    {
        SourceWriter.WriteSummaryComment(builder, attr.Brief, indent: 4);
        SourceWriter.WriteObsolete(builder, attr.Deprecated, indent: 4);

        var enumClassName = SourceWriter.ToPascalCase(attr.Key) + "Values";
        builder.Append("    public static class ").AppendLine(enumClassName);
        builder.AppendLine("    {");

        var first = true;
        foreach (var member in enumType.Members)
        {
            if (!StabilityFiltering.IsIncludedOrDeprecated(member.Stability, member.Deprecated ?? attr.Deprecated, filter))
                continue;

            if (!first) builder.AppendLine();
            first = false;
            WriteEnumMember(builder, member);
        }

        builder.AppendLine("    }");
    }

    private static void WriteEnumMember(StringBuilder builder, EnumMemberModel member)
    {
        var brief = string.IsNullOrEmpty(member.Brief) ? member.Id + "." : member.Brief;
        SourceWriter.WriteSummaryComment(builder, brief, indent: 8);
        SourceWriter.WriteObsolete(builder, member.Deprecated, indent: 8);

        var memberName = SourceWriter.ToPascalCase(member.Id);
        builder.Append("        public const string ").Append(memberName)
               .Append(" = \"").Append(member.Value).AppendLine("\";");
    }

    internal static string AttributeMemberName(string key, AttributeTypeModel type)
    {
        var pascal = SourceWriter.ToPascalCase(key);
        return type is AttributeTypeModel.Template
            ? "Attribute" + pascal + "Template"
            : "Attribute" + pascal;
    }
}
