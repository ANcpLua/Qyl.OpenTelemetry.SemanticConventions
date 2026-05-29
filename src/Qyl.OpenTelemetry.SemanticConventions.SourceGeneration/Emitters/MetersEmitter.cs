using System.Text;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits typed <c>System.Diagnostics.Metrics.Meter</c> factory wrappers for
/// every metric in the resolved registry whose <c>prefix</c> matches the marker
/// argument. Each wrapper is an extension method on <c>Meter</c> that returns
/// a strongly-typed instrument with the registry's name, unit, and description
/// embedded.
///
/// Per-metric stability is propagated to the extension method only for
/// <c>stability: deprecated</c>, which projects to <c>[Obsolete]</c>. This
/// matches contrib, Java, and Python upstream generators, which carry no
/// per-symbol annotation for non-stable tiers — stable/incubating separation
/// is handled at the Weaver-template/registry-filter layer, not the symbol
/// layer.
///
/// Audit (pinned-goal directive, Phase B-2): this emitter writes extension
/// methods on a consumer-provided <c>Meter</c> (<c>this Meter meter</c>) only.
/// No generated global <c>Meter</c> singletons, no
/// <c>private static readonly Meter ... = new Meter(...)</c>. Consumers own
/// runtime <c>Meter</c> instances and pick their own name/version/scope; the
/// generator only emits typed factory wrappers over the BCL surface.
/// </summary>
internal static class MetersEmitter
{
    public static FileWithName Generate(SemConvMarkerModel marker, InstrumentRegistryModel registry)
    {
        var meters = FilterByPrefix(registry, marker.Prefix, marker.Filter);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, meters);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static List<MetricDescriptorModel> FilterByPrefix(InstrumentRegistryModel registry, string prefix, StabilityFilter filter)
    {
        var result = new List<MetricDescriptorModel>();
        foreach (var meter in registry.Metrics)
        {
            if (!PrefixMatches(meter.MetricName, prefix))
                continue;

            // Stability gate. Deprecated rows survive every projection until
            // upstream drops them (contrib/Java/Python parity): the
            // [Obsolete] symbol stays so consumers can migrate at their pace.
            if (!StabilityFiltering.IsIncludedOrDeprecated(meter.Stability, meter.Deprecated, filter))
                continue;

            result.Add(meter);
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

    private static void WriteClass(StringBuilder builder, string className, List<MetricDescriptorModel> meters)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Typed factory extensions for OpenTelemetry semantic-convention metric instruments.");
        builder.AppendLine("/// Each method creates an instrument with the registry-defined name, unit, and description.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var meter in meters)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteFactory(builder, meter);
        }

        builder.AppendLine("}");
    }

    private static void WriteFactory(StringBuilder builder, MetricDescriptorModel meter)
    {
        SourceWriter.WriteSummaryComment(builder, meter.Brief, indent: 4);
        if (!string.IsNullOrEmpty(meter.Note))
            SourceWriter.WriteRemarksComment(builder, meter.Note, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, meter.Stability, meter.Deprecated, indent: 4);

        var methodName = "Create" + SourceWriter.ToPascalCase(meter.MetricName) + SourceWriter.ToPascalCase(meter.Instrument);
        var valueType = MeterValueTypeRules.SelectValueType(meter.Instrument, meter.Unit);
        var instrumentType = ResolveInstrumentType(meter.Instrument, valueType);

        switch (meter.Instrument)
        {
            case "gauge":
            case "observablegauge":
                builder.Append("    public static global::System.Diagnostics.Metrics.").Append(instrumentType)
                       .Append(' ').Append(methodName).AppendLine("(");
                builder.AppendLine("        this global::System.Diagnostics.Metrics.Meter meter,");
                builder.Append("        global::System.Func<").Append(valueType).AppendLine("> observeValue)");
                builder.Append("        => meter.CreateObservableGauge<").Append(valueType).AppendLine(">(");
                builder.Append("            name: \"").Append(meter.MetricName).AppendLine("\",");
                builder.AppendLine("            observeValue: observeValue,");
                builder.Append("            unit: \"").Append(SourceWriter.EscapeAttribute(meter.Unit)).AppendLine("\",");
                builder.Append("            description: \"").Append(SourceWriter.EscapeAttribute(meter.Brief)).AppendLine("\");");
                break;

            case "observablecounter":
                builder.Append("    public static global::System.Diagnostics.Metrics.").Append(instrumentType)
                       .Append(' ').Append(methodName).AppendLine("(");
                builder.AppendLine("        this global::System.Diagnostics.Metrics.Meter meter,");
                builder.Append("        global::System.Func<").Append(valueType).AppendLine("> observeValue)");
                builder.Append("        => meter.CreateObservableCounter<").Append(valueType).AppendLine(">(");
                builder.Append("            name: \"").Append(meter.MetricName).AppendLine("\",");
                builder.AppendLine("            observeValue: observeValue,");
                builder.Append("            unit: \"").Append(SourceWriter.EscapeAttribute(meter.Unit)).AppendLine("\",");
                builder.Append("            description: \"").Append(SourceWriter.EscapeAttribute(meter.Brief)).AppendLine("\");");
                break;

            case "observableupdowncounter":
                builder.Append("    public static global::System.Diagnostics.Metrics.").Append(instrumentType)
                       .Append(' ').Append(methodName).AppendLine("(");
                builder.AppendLine("        this global::System.Diagnostics.Metrics.Meter meter,");
                builder.Append("        global::System.Func<").Append(valueType).AppendLine("> observeValue)");
                builder.Append("        => meter.CreateObservableUpDownCounter<").Append(valueType).AppendLine(">(");
                builder.Append("            name: \"").Append(meter.MetricName).AppendLine("\",");
                builder.AppendLine("            observeValue: observeValue,");
                builder.Append("            unit: \"").Append(SourceWriter.EscapeAttribute(meter.Unit)).AppendLine("\",");
                builder.Append("            description: \"").Append(SourceWriter.EscapeAttribute(meter.Brief)).AppendLine("\");");
                break;

            default:
                builder.Append("    public static global::System.Diagnostics.Metrics.").Append(instrumentType)
                       .Append(' ').Append(methodName).AppendLine("(");
                builder.AppendLine("        this global::System.Diagnostics.Metrics.Meter meter)");
                builder.Append("        => meter.").Append(InstrumentFactoryMethod(meter.Instrument))
                       .Append('<').Append(valueType).AppendLine(">(");
                builder.Append("            name: \"").Append(meter.MetricName).AppendLine("\",");
                builder.Append("            unit: \"").Append(SourceWriter.EscapeAttribute(meter.Unit)).AppendLine("\",");
                builder.Append("            description: \"").Append(SourceWriter.EscapeAttribute(meter.Brief)).AppendLine("\");");
                break;
        }
    }

    private static string ResolveInstrumentType(string instrument, string valueType) => instrument switch
    {
        "histogram" => $"Histogram<{valueType}>",
        "counter" => $"Counter<{valueType}>",
        "updowncounter" => $"UpDownCounter<{valueType}>",
        "gauge" => $"ObservableGauge<{valueType}>",
        "observablegauge" => $"ObservableGauge<{valueType}>",
        "observablecounter" => $"ObservableCounter<{valueType}>",
        "observableupdowncounter" => $"ObservableUpDownCounter<{valueType}>",
        _ => $"Instrument<{valueType}>"
    };

    private static string InstrumentFactoryMethod(string instrument) => instrument switch
    {
        "histogram" => "CreateHistogram",
        "counter" => "CreateCounter",
        "updowncounter" => "CreateUpDownCounter",
        _ => "CreateHistogram"
    };
}
