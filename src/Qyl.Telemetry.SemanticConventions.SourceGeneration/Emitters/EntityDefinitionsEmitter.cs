using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits first-class <c>EntityDefinition</c> objects for every entity in the resolved
/// registry whose name matches the marker prefix. Stability, structured deprecation, and
/// the full describing/identifying attribute references (with requirement levels) travel
/// with the object. This is the standalone entity definition, distinct from the name-only
/// <c>EntityRef</c> a metric or span carries in its association list.
/// </summary>
internal static class EntityDefinitionsEmitter
{
    private const string Ns = SignalEmitterShared.Ns;

    public static FileWithName Generate(SemConvMarkerModel marker, SignalRegistryModel registry)
    {
        var entities = new List<EntityDescriptorModel>();
        foreach (var entity in registry.Entities)
        {
            if (!SignalEmitterShared.PrefixMatches(entity.Name, marker.Prefix))
                continue;
            if (!StabilityFiltering.IsIncludedOrDeprecated(entity.Stability, entity.Deprecated, marker.Filter))
                continue;
            entities.Add(entity);
        }

        var builder = new StringBuilder();
        SourceWriter.WriteHeader(builder);
        SourceWriter.WriteNamespace(builder, marker.ContainingNamespace);
        WriteClass(builder, marker.ClassName, entities);

        var fileName = GeneratedSourceNames.ForPartialType(marker.ContainingNamespace, marker.ClassName);
        return new FileWithName(fileName, builder.ToString());
    }

    private static void WriteClass(StringBuilder builder, string className, List<EntityDescriptorModel> entities)
    {
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// First-class OpenTelemetry semantic-convention entity definitions. Each field is a");
        builder.AppendLine("/// typed object carrying the entity name, stability, structured deprecation, and the");
        builder.AppendLine("/// describing/identifying attribute references from the resolved-registry pin.");
        builder.AppendLine("/// </summary>");
        builder.Append("static partial class ").AppendLine(className);
        builder.AppendLine("{");

        var first = true;
        foreach (var entity in entities)
        {
            if (!first) builder.AppendLine();
            first = false;
            WriteDefinition(builder, entity);
        }

        builder.AppendLine("}");
    }

    private static void WriteDefinition(StringBuilder builder, EntityDescriptorModel entity)
    {
        SourceWriter.WriteSummaryComment(builder, entity.Brief, indent: 4);
        if (!string.IsNullOrEmpty(entity.Note))
            SourceWriter.WriteRemarksComment(builder, entity.Note, indent: 4);
        SourceWriter.WriteStabilityObsolete(builder, entity.Stability, entity.Deprecated, indent: 4);

        var fieldName = SourceWriter.ToPascalCase(entity.Name);

        builder.Append("    public static readonly ").Append(Ns).Append(".EntityDefinition ")
               .Append(fieldName).AppendLine(" =");
        builder.Append("        new(").AppendLine();
        builder.Append("            name: \"").Append(SourceWriter.EscapeAttribute(entity.Name)).AppendLine("\",");
        builder.Append("            brief: \"").Append(SourceWriter.EscapeAttribute(entity.Brief)).AppendLine("\",");
        builder.Append("            stability: ").Append(SignalEmitterShared.StabilityExpr(entity.Stability)).AppendLine(",");
        builder.Append("            deprecation: ").Append(SignalEmitterShared.DeprecationExpr(entity.Deprecated)).AppendLine(",");
        builder.Append("            attributes: ").Append(SignalEmitterShared.AttributeArrayExpr(entity.Attributes)).AppendLine(");");
    }
}
