using System.Text;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits the source file completing a partial class marked with
/// <c>[SemanticConventionEvents("&lt;prefix&gt;")]</c>.
///
/// For each event group whose <c>event_name</c> starts with the requested prefix the emitter
/// produces:
/// <list type="bullet">
///   <item><description>A <c>public const string Event&lt;PascalName&gt; = "&lt;event.name&gt;";</c> constant.</description></item>
///   <item><description>A <c>public static partial class &lt;PascalName&gt;Descriptor</c> carrying
///         name, emission target, body metadata, and payload attribute metadata.</description></item>
///   <item><description>A <c>public readonly record struct &lt;PascalName&gt;Payload</c> whose properties
///         project the event's payload attributes (required attributes first, non-nullable;
///         recommended attributes after, nullable).</description></item>
/// </list>
/// </summary>
internal static class EventsEmitter
{
    // Event-emission target audit (semconv v1.41.0):
    // The upstream event rows do not carry a stable discriminator that says
    // "ActivityEvent" vs "Logger/Event". We preserve that fact explicitly as
    // EventGroupModel.EmissionTarget=Unspecified instead of generating
    // ActivityEvent-only helpers. The caller picks the emission API at use-site.

    public static FileWithName Generate(SemConvMarkerModel marker, InstrumentRegistryModel instruments)
    {
        var events = ResolveEvents(instruments, marker.Prefix, marker.Filter);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, events);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static List<EventGroupModel> ResolveEvents(InstrumentRegistryModel instruments, string prefix, StabilityFilter filter)
    {
        // Stability gating mirrors the contrib/Java/Python "stable package" vs "incubating
        // package" split — same semantics as MetricsEmitter.ResolveMetrics. Deprecated rows
        // stay emitted (with [Obsolete]) under whichever stability tier owns them, per
        // OTel telemetry-stability.md.
        var dotted = prefix + ".";
        var matched = new List<EventGroupModel>();
        foreach (var ev in instruments.Events)
        {
            if (!string.Equals(ev.EventName, prefix, StringComparison.Ordinal) &&
                !ev.EventName.StartsWith(dotted, StringComparison.Ordinal))
            {
                continue;
            }

            if (!StabilityFiltering.IsIncludedOrDeprecated(ev.Stability, ev.Deprecated, filter))
            {
                continue;
            }

            matched.Add(ev);
        }
        matched.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.EventName, b.EventName));
        return matched;
    }

    private static void WriteClass(StringBuilder builder, string className, List<EventGroupModel> events)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Constants for event names and typed payload structs outlined by the OpenTelemetry specifications.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var ev in events)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteEventConstant(builder, ev);
        }

        foreach (var ev in events)
        {
            builder.AppendLine();
            WriteDescriptorClass(builder, ev);
        }

        foreach (var ev in events)
        {
            builder.AppendLine();
            WritePayloadStruct(builder, ev);
        }

        builder.AppendLine("}");
    }

    private static void WriteEventConstant(StringBuilder builder, EventGroupModel ev)
    {
        SourceWriter.WriteSummaryComment(builder, ev.Brief, indent: 4);
        if (!string.IsNullOrEmpty(ev.Note))
            SourceWriter.WriteRemarksComment(builder, ev.Note, indent: 4);
        SourceWriter.WriteObsolete(builder, ev.Deprecated, indent: 4);

        var memberName = EventMemberName(ev.EventName);
        builder.Append("    public const string ").Append(memberName)
               .Append(" = \"").Append(ev.EventName).AppendLine("\";");
    }

    private static void WriteDescriptorClass(StringBuilder builder, EventGroupModel ev)
    {
        SourceWriter.WriteSummaryComment(builder, ev.Brief, indent: 4);
        if (!string.IsNullOrEmpty(ev.Note))
            SourceWriter.WriteRemarksComment(builder, ev.Note, indent: 4);
        SourceWriter.WriteObsolete(builder, ev.Deprecated, indent: 4);

        var descriptorName = DescriptorClassName(ev.EventName);
        builder.Append("    public static partial class ").AppendLine(descriptorName);
        builder.AppendLine("    {");
        builder.Append("        public const string Name = \"").Append(SourceWriter.EscapeLiteral(ev.EventName)).AppendLine("\";");
        builder.Append("        public const string Brief = \"").Append(SourceWriter.EscapeLiteral(ev.Brief)).AppendLine("\";");
        builder.Append("        public const string Note = \"").Append(SourceWriter.EscapeLiteral(ev.Note)).AppendLine("\";");
        builder.Append("        public const string EmissionTarget = \"").Append(EventTargetName(ev.EmissionTarget)).AppendLine("\";");
        builder.Append("        public const bool HasBody = ").Append(string.IsNullOrEmpty(ev.BodyJson) ? "false" : "true").AppendLine(";");
        builder.Append("        public const string BodyJson = \"").Append(SourceWriter.EscapeLiteral(ev.BodyJson)).AppendLine("\";");
        builder.Append("        public const int AttributeCount = ").Append(ev.Payload.Length).AppendLine(";");
        WriteEntityAssociations(builder, ev);

        foreach (var attr in ev.Payload)
        {
            var name = "Attribute" + SourceWriter.ToPascalCase(attr.Key);
            builder.Append("        public const string ").Append(name)
                   .Append(" = \"").Append(SourceWriter.EscapeLiteral(attr.Key)).AppendLine("\";");
            builder.Append("        public const string ").Append(name).Append("RequirementLevel")
                   .Append(" = \"").Append(SourceWriter.RequirementLevelName(attr.RequirementLevel.Kind)).AppendLine("\";");

            if (!string.IsNullOrEmpty(attr.RequirementLevel.Condition))
            {
                builder.Append("        public const string ").Append(name).Append("RequirementCondition")
                       .Append(" = \"").Append(SourceWriter.EscapeLiteral(attr.RequirementLevel.Condition)).AppendLine("\";");
            }

            if (!string.IsNullOrEmpty(attr.Note))
            {
                builder.Append("        public const string ").Append(name).Append("Note")
                       .Append(" = \"").Append(SourceWriter.EscapeLiteral(attr.Note)).AppendLine("\";");
            }

            WriteExampleConstants(builder, name, attr.Examples);
        }

        builder.AppendLine("    }");
    }

    private static void WriteEntityAssociations(StringBuilder builder, EventGroupModel ev)
    {
        builder.Append("        public const int EntityAssociationCount = ")
               .Append(ev.EntityAssociations.Length).AppendLine(";");

        foreach (var entity in ev.EntityAssociations)
        {
            builder.Append("        public const string EntityAssociation").Append(SourceWriter.ToPascalCase(entity))
                   .Append(" = \"").Append(SourceWriter.EscapeLiteral(entity)).AppendLine("\";");
        }
    }

    private static void WritePayloadStruct(StringBuilder builder, EventGroupModel ev)
    {
        var ordered = OrderPayload(ev.Payload);

        SourceWriter.WriteSummaryComment(builder, ev.Brief, indent: 4);
        SourceWriter.WriteObsolete(builder, ev.Deprecated, indent: 4);

        var structName = PayloadStructName(ev.EventName);
        builder.Append("    public readonly record struct ").AppendLine(structName);
        builder.AppendLine("    {");

        var first = true;
        foreach (var member in ordered)
        {
            if (!first) builder.AppendLine();
            first = false;
            WritePayloadProperty(builder, member);
        }

        builder.AppendLine("    }");
    }

    private static List<SignalAttributeModel> OrderPayload(EquatableArray<SignalAttributeModel> payload)
    {
        var required = new List<SignalAttributeModel>();
        var recommended = new List<SignalAttributeModel>();
        foreach (var member in payload)
        {
            if (member.RequirementLevel.Kind == RequirementLevelKind.Required) required.Add(member);
            else recommended.Add(member);
        }
        required.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        recommended.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        required.AddRange(recommended);
        return required;
    }

    private static void WritePayloadProperty(StringBuilder builder, SignalAttributeModel member)
    {
        SourceWriter.WriteSummaryComment(builder, member.Brief, indent: 8);
        WriteRequirementRemarks(builder, member, indent: 8);
        SourceWriter.WriteObsolete(builder, member.Deprecated, indent: 8);

        var propertyType = PropertyTypeName(member.Type, member.RequirementLevel.Kind == RequirementLevelKind.Required);
        var propertyName = PropertyName(member.Key);
        builder.Append("        public ").Append(propertyType).Append(' ')
               .Append(propertyName).AppendLine(" { get; init; }");
    }

    private static void WriteRequirementRemarks(StringBuilder builder, SignalAttributeModel member, int indent)
    {
        if (member.RequirementLevel.Kind == RequirementLevelKind.Unspecified &&
            string.IsNullOrEmpty(member.RequirementLevel.Condition) &&
            string.IsNullOrEmpty(member.Note) &&
            member.Examples.Length == 0)
        {
            return;
        }

        var pad = new string(' ', indent);
        builder.Append(pad).AppendLine("/// <remarks>");
        builder.Append(pad).Append("/// Requirement level: ")
               .Append(SourceWriter.RequirementLevelName(member.RequirementLevel.Kind)).AppendLine(".");
        if (!string.IsNullOrEmpty(member.RequirementLevel.Condition))
        {
            foreach (var line in SourceWriter.SplitLines(member.RequirementLevel.Condition))
            {
                SourceWriter.AppendDocLine(builder, pad, line);
            }
        }
        if (!string.IsNullOrEmpty(member.Note))
        {
            foreach (var line in SourceWriter.SplitLines(member.Note))
            {
                SourceWriter.AppendDocLine(builder, pad, line);
            }
        }
        if (member.Examples.Length > 0)
        {
            builder.Append(pad).AppendLine("/// Examples:");
            foreach (var example in member.Examples)
            {
                SourceWriter.AppendDocLine(builder, pad, "- " + example);
            }
        }
        builder.Append(pad).AppendLine("/// </remarks>");
    }

    private static void WriteExampleConstants(
        StringBuilder builder,
        string memberPrefix,
        EquatableArray<string> examples)
    {
        if (examples.Length == 0)
            return;

        builder.Append("        public const int ").Append(memberPrefix).Append("ExampleCount = ")
               .Append(examples.Length).AppendLine(";");
        for (var i = 0; i < examples.Length; i++)
        {
            builder.Append("        public const string ").Append(memberPrefix).Append("Example")
                   .Append(i + 1).Append(" = \"").Append(SourceWriter.EscapeLiteral(examples[i])).AppendLine("\";");
        }
    }

    internal static string EventMemberName(string eventName) => "Event" + SourceWriter.ToPascalCase(eventName);

    internal static string PayloadStructName(string eventName) => SourceWriter.ToPascalCase(eventName) + "Payload";

    internal static string DescriptorClassName(string eventName) => SourceWriter.ToPascalCase(eventName) + "Descriptor";

    internal static string PropertyName(string attributeKey) => SourceWriter.ToPascalCase(attributeKey);

    private static string PropertyTypeName(AttributeTypeModel type, bool required)
    {
        var baseType = type switch
        {
            AttributeTypeModel.Primitive p => MapPrimitive(p.Name),
            AttributeTypeModel.Template => "string",
            AttributeTypeModel.EnumType => "string",
            _ => "string"
        };

        return required ? baseType : baseType + "?";
    }

    private static string MapPrimitive(string name) => name switch
    {
        "string" => "string",
        "int" => "long",
        "double" => "double",
        "boolean" => "bool",
        "string[]" => "global::System.Collections.Generic.IReadOnlyList<string>",
        "int[]" => "global::System.Collections.Generic.IReadOnlyList<long>",
        "double[]" => "global::System.Collections.Generic.IReadOnlyList<double>",
        "boolean[]" => "global::System.Collections.Generic.IReadOnlyList<bool>",
        _ => "string"
    };

    private static string EventTargetName(EventEmissionTargetModel target) => target switch
    {
        EventEmissionTargetModel.ActivityEvent => "activity_event",
        EventEmissionTargetModel.LogRecord => "log_record",
        _ => "unspecified"
    };
}
