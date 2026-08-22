using System.Text;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Shared C# expression rendering for the first-class definition emitters
/// (metric, span, event). Keeps the stability / structured-deprecation /
/// attribute-reference projections identical across all three surfaces.
/// </summary>
internal static class SignalEmitterShared
{
    public const string Ns = "global::Qyl.Telemetry.SemanticConventions";

    public static bool PrefixMatches(string name, string prefix)
    {
        if (name.EqualsOrdinal(prefix)) return true;
        if (name.Length <= prefix.Length) return false;
        if (!name.StartsWithOrdinal(prefix)) return false;
        return name[prefix.Length] == '.';
    }

    public static string StabilityExpr(StabilityModel stability) => Ns + ".Stability." + stability switch
    {
        StabilityModel.Stable => "Stable",
        StabilityModel.ReleaseCandidate => "ReleaseCandidate",
        StabilityModel.Beta => "Beta",
        StabilityModel.Alpha => "Alpha",
        StabilityModel.Deprecated => "Deprecated",
        _ => "Development",
    };

    public static string DeprecationExpr(DeprecatedModel? deprecated) => deprecated switch
    {
        null => Ns + ".Deprecation.None",
        DeprecatedModel.Renamed r => Ns + ".Deprecation.Renamed(\"" + SourceWriter.EscapeAttribute(r.RenamedTo) + "\")",
        DeprecatedModel.Obsoleted => Ns + ".Deprecation.Obsoleted",
        DeprecatedModel.Uncategorized u => Ns + ".Deprecation.Uncategorized(\"" + SourceWriter.EscapeAttribute(u.Note) + "\")",
        _ => Ns + ".Deprecation.None",
    };

    public static string AttributeArrayExpr(EquatableArray<SignalAttributeModel> attributes)
    {
        if (attributes.IsEmpty)
            return "global::System.Array.Empty<" + Ns + ".AttributeRef>()";

        var sb = new StringBuilder("new " + Ns + ".AttributeRef[] { ");
        var first = true;
        foreach (var a in attributes)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append("new(\"").Append(SourceWriter.EscapeAttribute(a.Key)).Append("\", \"")
              .Append(AttributeTypeName(a.Type)).Append("\", ")
              .Append(RequirementLevelExpr(a.RequirementLevel.Kind)).Append(", ")
              .Append(a.Deprecated is not null ? "true" : "false").Append(')');
        }
        sb.Append(" }");
        return sb.ToString();
    }

    private static string AttributeTypeName(AttributeTypeModel type) => type switch
    {
        AttributeTypeModel.Primitive p => p.Name,
        AttributeTypeModel.EnumType => "enum",
        AttributeTypeModel.Template => "template",
        _ => "string",
    };

    private static string RequirementLevelExpr(RequirementLevelKind kind) => Ns + ".RequirementLevel." + kind switch
    {
        RequirementLevelKind.Required => "Required",
        RequirementLevelKind.Recommended => "Recommended",
        RequirementLevelKind.OptIn => "OptIn",
        RequirementLevelKind.ConditionallyRequired => "ConditionallyRequired",
        _ => "Unspecified",
    };
}
