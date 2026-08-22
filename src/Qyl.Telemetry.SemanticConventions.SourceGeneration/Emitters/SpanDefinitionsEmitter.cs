using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits first-class <c>SpanDefinition&lt;TKind&gt;</c> objects for every span group in the
/// resolved registry whose id matches the marker prefix. The span kind is a marker type
/// (compile-time safety), and stability, structured deprecation, and attribute references
/// (with requirement levels) travel with the object.
/// </summary>
internal static class SpanDefinitionsEmitter
{
    private const string Ns = SignalEmitterShared.Ns;

    public static FileWithName Generate(SemConvMarkerModel marker, SignalRegistryModel registry)
    {
        var spans = new List<SpanDescriptorModel>();
        foreach (var span in registry.Spans)
        {
            if (!SignalEmitterShared.PrefixMatches(span.Id, marker.Prefix))
                continue;
            if (!StabilityFiltering.IsIncludedOrDeprecated(span.Stability, span.Deprecated, marker.Filter))
                continue;
            spans.Add(span);
        }

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, spans);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static void WriteClass(StringBuilder builder, string className, List<SpanDescriptorModel> spans)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// First-class OpenTelemetry semantic-convention span definitions. Each field is a");
        builder.AppendLine("/// typed object carrying the span id, span kind, stability, structured deprecation,");
        builder.AppendLine("/// and attribute references from the resolved-registry pin.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var span in spans)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteDefinition(builder, span);
        }

        builder.AppendLine("}");
    }

    private static void WriteDefinition(StringBuilder builder, SpanDescriptorModel span)
    {
        SourceWriter.WriteSummaryComment(builder, span.Brief, indent: 4);
        if (!string.IsNullOrEmpty(span.Note))
            SourceWriter.WriteRemarksComment(builder, span.Note, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, span.Stability, span.Deprecated, indent: 4);

        var fieldName = SourceWriter.ToPascalCase(span.Id);
        var kind = SpanKindMarker(span.SpanKind);

        builder.Append("    public static readonly ").Append(Ns).Append(".SpanDefinition<").Append(Ns).Append('.')
               .Append(kind).Append("> ").Append(fieldName).AppendLine(" =");
        builder.Append("        new(").AppendLine();
        builder.Append("            id: \"").Append(SourceWriter.EscapeAttribute(span.Id)).AppendLine("\",");
        builder.Append("            brief: \"").Append(SourceWriter.EscapeAttribute(span.Brief)).AppendLine("\",");
        builder.Append("            stability: ").Append(SignalEmitterShared.StabilityExpr(span.Stability)).AppendLine(",");
        builder.Append("            deprecation: ").Append(SignalEmitterShared.DeprecationExpr(span.Deprecated)).AppendLine(",");
        builder.Append("            attributes: ").Append(SignalEmitterShared.AttributeArrayExpr(span.Attributes)).AppendLine(");");
    }

    private static string SpanKindMarker(string spanKind) => spanKind switch
    {
        "client" => "Client",
        "server" => "Server",
        "producer" => "Producer",
        "consumer" => "Consumer",
        _ => "Internal",
    };
}
