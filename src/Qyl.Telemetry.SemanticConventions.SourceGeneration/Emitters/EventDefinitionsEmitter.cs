using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits first-class <c>EventDefinition</c> objects for every event in the resolved
/// registry whose name matches the marker prefix. Stability, structured deprecation, and
/// attribute references (with requirement levels) travel with the object. Events have no
/// kind, so the type is not generic.
/// </summary>
internal static class EventDefinitionsEmitter
{
    private const string Ns = SignalEmitterShared.Ns;

    public static FileWithName Generate(SemConvMarkerModel marker, SignalRegistryModel registry)
    {
        var events = new List<EventDescriptorModel>();
        foreach (var ev in registry.Events)
        {
            if (!SignalEmitterShared.PrefixMatches(ev.EventName, marker.Prefix))
                continue;
            if (!StabilityFiltering.IsIncludedOrDeprecated(ev.Stability, ev.Deprecated, marker.Filter))
                continue;
            events.Add(ev);
        }

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, events);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static void WriteClass(StringBuilder builder, string className, List<EventDescriptorModel> events)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// First-class OpenTelemetry semantic-convention event definitions. Each field is a");
        builder.AppendLine("/// typed object carrying the event name, stability, structured deprecation, and");
        builder.AppendLine("/// attribute references from the resolved-registry pin.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var ev in events)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteDefinition(builder, ev);
        }

        builder.AppendLine("}");
    }

    private static void WriteDefinition(StringBuilder builder, EventDescriptorModel ev)
    {
        SourceWriter.WriteSummaryComment(builder, ev.Brief, indent: 4);
        if (!string.IsNullOrEmpty(ev.Note))
            SourceWriter.WriteRemarksComment(builder, ev.Note, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, ev.Stability, ev.Deprecated, indent: 4);

        var fieldName = SourceWriter.ToPascalCase(ev.EventName);

        builder.Append("    public static readonly ").Append(Ns).Append(".EventDefinition ")
               .Append(fieldName).AppendLine(" =");
        builder.Append("        new(").AppendLine();
        builder.Append("            name: \"").Append(SourceWriter.EscapeAttribute(ev.EventName)).AppendLine("\",");
        builder.Append("            brief: \"").Append(SourceWriter.EscapeAttribute(ev.Brief)).AppendLine("\",");
        builder.Append("            stability: ").Append(SignalEmitterShared.StabilityExpr(ev.Stability)).AppendLine(",");
        builder.Append("            deprecation: ").Append(SignalEmitterShared.DeprecationExpr(ev.Deprecated)).AppendLine(",");
        builder.Append("            attributes: ").Append(SignalEmitterShared.AttributeArrayExpr(ev.Attributes)).AppendLine(");");
    }
}
