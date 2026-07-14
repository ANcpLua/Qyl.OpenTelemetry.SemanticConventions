using System.Text;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits typed <c>System.Diagnostics.Activity</c> setter extensions for every
/// semconv attribute matching the marker's prefix. Each setter calls
/// <c>Activity.SetTag(string, object?)</c> with the registry-defined key and
/// the correctly-typed value.
///
/// Stability is propagated per-attribute only for <c>stability: deprecated</c>,
/// which projects to <c>[Obsolete]</c>. This matches contrib, Java, and Python
/// upstream generators, which carry no per-symbol annotation for non-stable
/// tiers — stable/incubating separation is handled at the Weaver-template/
/// registry-filter layer, not the symbol layer. For <c>template[...]</c>
/// attributes the setter takes a <c>string segment</c> parameter to compose
/// the runtime key. For enum-typed attributes the registry's enum values are
/// emitted as a nested static class of <c>const string</c>s so call sites are
/// discoverable (e.g.
/// <c>activity.SetHttpRequestMethod(HttpActivityExtensions.HttpRequestMethodValues.Get)</c>).
/// Consumers own the <c>ActivitySource</c>/<c>Activity</c> lifecycle; generated
/// code contains only typed extensions over the BCL surface.
/// </summary>
internal static class ActivityExtensionsEmitter
{
    public static FileWithName Generate(SemConvMarkerModel marker, ActivityRegistryModel registry)
    {
        var attributes = FilterByPrefix(registry, marker.Prefix, marker.Filter);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, attributes, marker.Filter);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static List<ActivityAttributeModel> FilterByPrefix(ActivityRegistryModel registry, string prefix, StabilityFilter filter)
    {
        var result = new List<ActivityAttributeModel>();
        foreach (var attr in registry.Attributes)
        {
            if (!PrefixMatches(attr.Key, prefix))
                continue;

            // Stability gate. Deprecated rows survive every projection until
            // upstream drops them (contrib/Java/Python parity): the
            // [Obsolete] symbol stays so consumers can migrate at their pace.
            if (!StabilityFiltering.IsIncludedOrDeprecated(attr.Stability, attr.Deprecated, filter))
                continue;

            result.Add(attr);
        }
        result.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        return result;
    }

    private static bool PrefixMatches(string key, string prefix)
    {
        if (key.EqualsOrdinal(prefix)) return true;
        if (key.Length <= prefix.Length) return false;
        if (!key.StartsWithOrdinal(prefix)) return false;
        return key[prefix.Length] == '.';
    }

    private static void WriteClass(
        StringBuilder builder,
        string className,
        List<ActivityAttributeModel> attributes,
        StabilityFilter filter)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Typed setter extensions for OpenTelemetry semantic-convention attributes on a span.");
        builder.AppendLine("/// Each method invokes <c>global::System.Diagnostics.Activity.SetTag</c> with the");
        builder.AppendLine("/// registry-defined key and a strongly-typed value.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var attr in attributes)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteSetter(builder, attr);
        }

        foreach (var attr in attributes)
        {
            if (!attr.IsEnum) continue;
            builder.AppendLine();
            WriteEnumValueClass(builder, attr, filter);
        }

        builder.AppendLine("}");
    }

    private static void WriteSetter(StringBuilder builder, ActivityAttributeModel attr)
    {
        SourceWriter.WriteSummaryComment(builder, attr.Brief, indent: 4);
        if (!string.IsNullOrEmpty(attr.Note))
            SourceWriter.WriteRemarksComment(builder, attr.Note, indent: 4);
        SourceWriter.WriteExamplesComment(builder, attr.Examples, indent: 4);
        WriteContextsComment(builder, attr.Contexts, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, attr.Stability, attr.Deprecated, indent: 4);

        var methodName = "Set" + SourceWriter.ToPascalCase(attr.Key);
        var parameterType = attr.CSharpParameterType;

        if (attr.IsTemplate)
        {
            builder.Append("    public static global::System.Diagnostics.Activity ")
                   .Append(methodName).AppendLine("(");
            builder.AppendLine("        this global::System.Diagnostics.Activity activity,");
            builder.AppendLine("        string segment,");
            builder.Append("        ").Append(parameterType).AppendLine(" value)");
            builder.Append("        => activity.SetTag($\"").Append(attr.Key).AppendLine(".{segment}\", value);");
        }
        else
        {
            builder.Append("    public static global::System.Diagnostics.Activity ")
                   .Append(methodName).AppendLine("(");
            builder.AppendLine("        this global::System.Diagnostics.Activity activity,");
            builder.Append("        ").Append(parameterType).AppendLine(" value)");
            builder.Append("        => activity.SetTag(\"").Append(attr.Key).AppendLine("\", value);");
        }
    }

    private static void WriteEnumValueClass(StringBuilder builder, ActivityAttributeModel attr, StabilityFilter filter)
    {
        SourceWriter.WriteSummaryComment(builder, attr.Brief, indent: 4);
        if (!string.IsNullOrEmpty(attr.Note))
            SourceWriter.WriteRemarksComment(builder, attr.Note, indent: 4);
        SourceWriter.WriteExamplesComment(builder, attr.Examples, indent: 4);
        WriteContextsComment(builder, attr.Contexts, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, attr.Stability, attr.Deprecated, indent: 4);

        var enumClassName = SourceWriter.ToPascalCase(attr.Key) + "Values";
        builder.Append("    public static class ").AppendLine(enumClassName);
        builder.AppendLine("    {");

        var first = true;
        foreach (var member in attr.EnumMembers)
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

    private static void WriteContextsComment(
        StringBuilder builder,
        EquatableArray<ActivityAttributeContextModel> contexts,
        int indent)
    {
        if (contexts.Length == 0)
            return;

        var writtenHeader = false;
        var pad = new string(' ', indent);
        foreach (var context in contexts)
        {
            if (context.RequirementLevel.Kind == RequirementLevelKind.Unspecified &&
                string.IsNullOrEmpty(context.RequirementLevel.Condition))
            {
                continue;
            }

            if (!writtenHeader)
            {
                builder.Append(pad).AppendLine("/// <remarks>");
                builder.Append(pad).AppendLine("/// Semantic-convention contexts:");
                writtenHeader = true;
            }

            var prefix = string.IsNullOrEmpty(context.Prefix) ? "<none>" : context.Prefix;
            var line = "- " + context.GroupId + " (" + context.GroupType + ", prefix " + prefix + "): " +
                       SourceWriter.RequirementLevelName(context.RequirementLevel.Kind);
            if (!string.IsNullOrEmpty(context.RequirementLevel.Condition))
                line += " - " + context.RequirementLevel.Condition;

            // Condition is free-form and may be multi-line; split so it stays inside
            // the doc comment instead of leaking raw lines into source.
            foreach (var physical in SourceWriter.SplitLines(line))
                SourceWriter.AppendDocLine(builder, pad, physical);
        }

        if (writtenHeader)
            builder.Append(pad).AppendLine("/// </remarks>");
    }
}
