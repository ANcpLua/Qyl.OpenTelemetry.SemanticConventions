using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits first-class <c>MetricDefinition&lt;TInstrument&gt;</c> objects for every metric
/// in the resolved registry whose name matches the marker prefix. Unlike
/// <see cref="MetersEmitter"/> — which projects a name+unit factory and flattens
/// deprecation to <c>[Obsolete]</c> — this surface preserves the whole registry fact:
/// instrument (as a marker type, for compile-time safety), unit, stability, entity
/// associations, and structured deprecation (renamed target / obsoleted / uncategorized).
/// The name becomes one property of the object rather than its identity.
/// </summary>
internal static class MetricDefinitionsEmitter
{
    private const string Ns = "global::Qyl.Telemetry.SemanticConventions";

    public static FileWithName Generate(SemConvMarkerModel marker, InstrumentRegistryModel registry)
    {
        var metrics = FilterByPrefix(registry, marker.Prefix, marker.Filter);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, metrics);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static List<MetricDescriptorModel> FilterByPrefix(InstrumentRegistryModel registry, string prefix, StabilityFilter filter)
    {
        var result = new List<MetricDescriptorModel>();
        foreach (var metric in registry.Metrics)
        {
            if (!PrefixMatches(metric.MetricName, prefix))
                continue;
            if (!StabilityFiltering.IsIncludedOrDeprecated(metric.Stability, metric.Deprecated, filter))
                continue;
            result.Add(metric);
        }
        result.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.MetricName, b.MetricName));
        return result;
    }

    private static bool PrefixMatches(string metricName, string prefix)
    {
        if (metricName.EqualsOrdinal(prefix)) return true;
        if (metricName.Length <= prefix.Length) return false;
        if (!metricName.StartsWithOrdinal(prefix)) return false;
        return metricName[prefix.Length] == '.';
    }

    private static void WriteClass(StringBuilder builder, string className, List<MetricDescriptorModel> metrics)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// First-class OpenTelemetry semantic-convention metric definitions. Each field is a");
        builder.AppendLine("/// typed object carrying the canonical name, instrument, unit, stability, entity");
        builder.AppendLine("/// associations, and structured deprecation from the resolved-registry pin.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var metric in metrics)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteDefinition(builder, metric);
        }

        builder.AppendLine("}");
    }

    private static void WriteDefinition(StringBuilder builder, MetricDescriptorModel metric)
    {
        SourceWriter.WriteSummaryComment(builder, metric.Brief, indent: 4);
        if (!string.IsNullOrEmpty(metric.Note))
            SourceWriter.WriteRemarksComment(builder, metric.Note, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, metric.Stability, metric.Deprecated, indent: 4);

        var fieldName = SourceWriter.ToPascalCase(metric.MetricName);
        var instrument = InstrumentMarker(metric.Instrument);

        builder.Append("    public static readonly ").Append(Ns).Append(".MetricDefinition<").Append(Ns).Append('.')
               .Append(instrument).Append("> ").Append(fieldName).AppendLine(" =");
        builder.Append("        new(").AppendLine();
        builder.Append("            name: \"").Append(SourceWriter.EscapeAttribute(metric.MetricName)).AppendLine("\",");
        builder.Append("            unit: \"").Append(SourceWriter.EscapeAttribute(metric.Unit)).AppendLine("\",");
        builder.Append("            brief: \"").Append(SourceWriter.EscapeAttribute(metric.Brief)).AppendLine("\",");
        builder.Append("            stability: ").Append(StabilityExpr(metric.Stability)).AppendLine(",");
        builder.Append("            deprecation: ").Append(DeprecationExpr(metric.Deprecated)).AppendLine(",");
        builder.Append("            entityAssociations: ").Append(EntityArrayExpr(metric.EntityAssociations)).AppendLine(");");
    }

    private static string InstrumentMarker(string instrument) => instrument switch
    {
        "counter" or "observablecounter" => "Counter",
        "updowncounter" or "observableupdowncounter" => "UpDownCounter",
        "gauge" or "observablegauge" => "Gauge",
        "histogram" => "Histogram",
        _ => "Counter",
    };

    private static string StabilityExpr(StabilityModel stability) => Ns + ".Stability." + stability switch
    {
        StabilityModel.Stable => "Stable",
        StabilityModel.ReleaseCandidate => "ReleaseCandidate",
        StabilityModel.Beta => "Beta",
        StabilityModel.Alpha => "Alpha",
        StabilityModel.Deprecated => "Deprecated",
        _ => "Development",
    };

    private static string DeprecationExpr(DeprecatedModel? deprecated) => deprecated switch
    {
        null => Ns + ".Deprecation.None",
        DeprecatedModel.Renamed r => Ns + ".Deprecation.Renamed(\"" + SourceWriter.EscapeAttribute(r.RenamedTo) + "\")",
        DeprecatedModel.Obsoleted => Ns + ".Deprecation.Obsoleted",
        DeprecatedModel.Uncategorized u => Ns + ".Deprecation.Uncategorized(\"" + SourceWriter.EscapeAttribute(u.Note) + "\")",
        _ => Ns + ".Deprecation.None",
    };

    private static string EntityArrayExpr(EquatableArray<string> entities)
    {
        if (entities.IsEmpty)
            return "global::System.Array.Empty<string>()";

        var sb = new StringBuilder("new string[] { ");
        var first = true;
        foreach (var e in entities)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append('"').Append(SourceWriter.EscapeAttribute(e)).Append('"');
        }
        sb.Append(" }");
        return sb.ToString();
    }
}
