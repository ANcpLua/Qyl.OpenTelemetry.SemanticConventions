using System.Text;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// The value-set members every generated <c>…Values</c> class carries after its constants,
/// so consumers validate against the vocabulary instead of retyping it:
/// <c>AllValues</c> (every catalogued value, in the order of that projection's constants) and
/// <c>Contains</c> (ordinal membership). Emitted identically by the package projection and
/// both consumer projections (attributes, activities). Shape rules: fully qualified BCL names
/// so the code needs no usings; string literals in the initializer so referencing an
/// <c>[Obsolete]</c> constant never raises CS0618; <c>Array.Empty</c> when the projection
/// emitted no constants; no LINQ and nothing beyond netstandard2.0.
/// </summary>
/// <remarks>
/// The property is <c>AllValues</c>, not <c>All</c>: <c>cassandra.consistency.level</c> has a
/// member <c>all</c> whose constant is <c>All</c>. Any PascalCased member id can collide with a
/// fixed name, so <see cref="Lines"/> refuses a class whose constants already use one of the
/// two names; the registry is pinned, which makes that a deterministic generation-time fault
/// rather than a consumer build break after a registry bump.
/// </remarks>
internal static class EnumValueSet
{
    public const string AllValuesName = "AllValues";
    public const string ContainsName = "Contains";

    public const string RegistryOrderSummary = "Every catalogued value, in registry order.";
    public const string DeclarationOrderSummary = "Every catalogued value, in the order the constants are declared.";

    /// <summary>
    /// The member lines, unindented; the caller applies the class's member indent.
    /// <paramref name="identifiers"/> are the constant names already declared in the class,
    /// in the same order as <paramref name="values"/>.
    /// </summary>
    public static string[] Lines(IReadOnlyList<string> identifiers, IReadOnlyList<string> values, string owner, string summary)
    {
        foreach (var identifier in identifiers)
        {
            if (identifier.EqualsOrdinal(AllValuesName) || identifier.EqualsOrdinal(ContainsName))
            {
                throw new InvalidOperationException(
                    $"Enum value class '{owner}' declares a constant '{identifier}', which collides with the generated value-set member of the same name.");
            }
        }

        return new[]
        {
            "/// <summary>" + summary + "</summary>",
            "public static global::System.Collections.Generic.IReadOnlyList<string> " + AllValuesName + " { get; } = " + ArrayExpression(values) + ";",
            "/// <summary>Whether <paramref name=\"value\"/> is a catalogued value (ordinal).</summary>",
            "public static bool " + ContainsName + "(string value) { foreach (var candidate in " + AllValuesName + ") if (string.Equals(candidate, value, global::System.StringComparison.Ordinal)) return true; return false; }",
        };
    }

    private static string ArrayExpression(IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return "global::System.Array.Empty<string>()";

        var builder = new StringBuilder("new[] { ");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append('"').Append(values[i]).Append('"');
        }
        return builder.Append(" }").ToString();
    }
}
