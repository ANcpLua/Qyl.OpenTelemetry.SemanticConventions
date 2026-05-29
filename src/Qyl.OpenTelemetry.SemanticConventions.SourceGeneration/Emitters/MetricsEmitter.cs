using System.Text;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits the source file completing a partial class marked with
/// <c>[SemanticConventionMetrics("&lt;prefix&gt;")]</c>.
///
/// For each metric group whose <c>metric_name</c> starts with the requested prefix the emitter
/// produces:
/// <list type="bullet">
///   <item><description>A <c>public const string Metric&lt;PascalName&gt; = "&lt;metric.name&gt;";</c> constant.</description></item>
///   <item><description>A <c>public static partial class &lt;PascalName&gt;Descriptor</c> carrying
///         <c>Name</c>, <c>Unit</c>, <c>Instrument</c>, documentation, attributes,
///         examples, and entity associations.</description></item>
/// </list>
/// </summary>
internal static class MetricsEmitter
{
    public static FileWithName Generate(SemConvMarkerModel marker, InstrumentRegistryModel instruments)
    {
        var metrics = ResolveMetrics(instruments, marker.Prefix, marker.Filter);

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, metrics);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static List<MetricDescriptorModel> ResolveMetrics(InstrumentRegistryModel instruments, string prefix, StabilityFilter filter)
    {
        // Stability gating mirrors the contrib/Java/Python "stable package" vs "incubating
        // package" split: under StableOnly we emit only stability=stable rows, under
        // AllStabilities we emit every row (stable + development + release_candidate + ...).
        // Per OTel telemetry-stability.md, deprecated rows stay emitted (with [Obsolete])
        // until upstream drops them — DeprecatedModel is orthogonal to StabilityModel here,
        // so a stable+deprecated row stays under StableOnly while a development+deprecated
        // row only appears under AllStabilities.
        var dotted = prefix + ".";
        var matched = new List<MetricDescriptorModel>();
        foreach (var metric in instruments.Metrics)
        {
            if (!string.Equals(metric.MetricName, prefix, StringComparison.Ordinal) &&
                !metric.MetricName.StartsWith(dotted, StringComparison.Ordinal))
            {
                continue;
            }

            if (!StabilityFiltering.IsIncludedOrDeprecated(metric.Stability, metric.Deprecated, filter))
            {
                continue;
            }

            matched.Add(metric);
        }
        matched.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.MetricName, b.MetricName));
        return matched;
    }

    private static void WriteClass(StringBuilder builder, string className, List<MetricDescriptorModel> metrics)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// Constants for metric names and descriptors outlined by the OpenTelemetry specifications.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var metric in metrics)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteMetricConstant(builder, metric);
        }

        foreach (var metric in metrics)
        {
            builder.AppendLine();
            WriteDescriptorClass(builder, metric);
        }

        builder.AppendLine("}");
    }

    private static void WriteMetricConstant(StringBuilder builder, MetricDescriptorModel metric)
    {
        SourceWriter.WriteSummaryComment(builder, metric.Brief, indent: 4);
        SourceWriter.WriteObsolete(builder, metric.Deprecated, indent: 4);

        var memberName = MetricMemberName(metric.MetricName);
        builder.Append("    public const string ").Append(memberName)
               .Append(" = \"").Append(metric.MetricName).AppendLine("\";");
    }

    private static void WriteDescriptorClass(StringBuilder builder, MetricDescriptorModel metric)
    {
        SourceWriter.WriteSummaryComment(builder, metric.Brief, indent: 4);
        if (!string.IsNullOrEmpty(metric.Note))
            SourceWriter.WriteRemarksComment(builder, metric.Note, indent: 4);
        SourceWriter.WriteObsolete(builder, metric.Deprecated, indent: 4);

        var descriptorName = DescriptorClassName(metric.MetricName);
        builder.Append("    public static partial class ").AppendLine(descriptorName);
        builder.AppendLine("    {");
        builder.Append("        public const string Name = \"").Append(metric.MetricName).AppendLine("\";");
        builder.Append("        public const string Unit = \"").Append(SourceWriter.EscapeLiteral(metric.Unit)).AppendLine("\";");
        builder.Append("        public const string Instrument = \"").Append(SourceWriter.EscapeLiteral(metric.Instrument)).AppendLine("\";");
        builder.Append("        public const string RequirementLevel = \"")
               .Append(SourceWriter.RequirementLevelName(metric.MetricRequirementLevel.Kind)).AppendLine("\";");
        if (!string.IsNullOrEmpty(metric.MetricRequirementLevel.Condition))
        {
            builder.Append("        public const string RequirementCondition = \"")
                   .Append(SourceWriter.EscapeLiteral(metric.MetricRequirementLevel.Condition)).AppendLine("\";");
        }
        builder.Append("        public const string Brief = \"").Append(SourceWriter.EscapeLiteral(metric.Brief)).AppendLine("\";");
        builder.Append("        public const string Note = \"").Append(SourceWriter.EscapeLiteral(metric.Note)).AppendLine("\";");
        builder.AppendLine();
        WriteDescriptorAttributes(builder, metric);
        WriteEntityAssociations(builder, metric);
        builder.AppendLine("    }");
    }

    private static void WriteDescriptorAttributes(StringBuilder builder, MetricDescriptorModel metric)
    {
        builder.Append("        public const int AttributeCount = ").Append(metric.Attributes.Length).AppendLine(";");

        foreach (var attr in metric.Attributes)
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

    private static void WriteEntityAssociations(StringBuilder builder, MetricDescriptorModel metric)
    {
        builder.AppendLine();
        builder.Append("        public const int EntityAssociationCount = ").Append(metric.EntityAssociations.Length).AppendLine(";");

        foreach (var entity in metric.EntityAssociations)
        {
            builder.Append("        public const string EntityAssociation").Append(SourceWriter.ToPascalCase(entity))
                   .Append(" = \"").Append(SourceWriter.EscapeLiteral(entity)).AppendLine("\";");
        }
    }

    internal static string MetricMemberName(string metricName) => "Metric" + SourceWriter.ToPascalCase(metricName);

    internal static string DescriptorClassName(string metricName) => SourceWriter.ToPascalCase(metricName) + "Descriptor";
}
