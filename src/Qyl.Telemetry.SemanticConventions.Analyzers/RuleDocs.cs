using System.IO;
using System.Text.RegularExpressions;

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     Help-link URL composition for diagnostic descriptors. Lives outside
///     <see cref="AlAnalyzer"/> so the docs generator can call it without
///     having to resolve <c>ANcpLua.Roslyn.Utilities</c> (the assembly that
///     defines <c>AlAnalyzer</c>'s base type). Single source of truth for
///     "what URL does QYL00XX point at?" — descriptors call it, the docs
///     generator calls it, the <c>--check</c> mode verifies them against
///     each other.
/// </summary>
internal static class RuleDocs
{
    /// <summary>
    ///     Base URL for diagnostic help links. Resolves to a per-rule page under
    ///     <c>docs/rules/QYL00XX_&lt;SymbolicName&gt;.md</c>, emitted by
    ///     <c>tools/Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator</c>.
    /// </summary>
    public const string HelpLinkBase =
        "https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions"
        + "/blob/main/docs/rules/";

    /// <summary>
    ///     Composes the full help-link URL for a diagnostic ID + symbolic name.
    ///     Symbolic name is the analyzer class-name suffix after stripping the
    ///     <c>(QYL|Qyl|AL|Al)NNNN</c> prefix and <c>Analyzer</c> suffix.
    /// </summary>
    public static string HelpLink(string id, string symbolicName) =>
        HelpLinkBase + id + "_" + symbolicName + ".md";

    /// <summary>
    ///     Derives the symbolic part of the per-rule docs filename from an analyzer's
    ///     source file path (supplied by the compiler via <c>CallerFilePath</c>).
    ///     The transform is intentionally tolerant of either <c>QYL</c> or <c>Qyl</c>
    ///     prefixing so renames don't break the URL.
    /// </summary>
    public static string SymbolicNameFromFile(string callerFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(callerFilePath);
        if (name.EndsWith("Analyzer", System.StringComparison.Ordinal))
            name = name.Substring(0, name.Length - "Analyzer".Length);
        var prefix = Regex.Match(name, "^(?:QYL|Qyl|AL|Al)\\d{4}");
        if (prefix.Success)
            name = name.Substring(prefix.Length);
        return name;
    }
}
