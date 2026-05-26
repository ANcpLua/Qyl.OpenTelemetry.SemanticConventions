// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   Class-name ↔ symbolic-name ↔ on-disk-filename transforms. Kept in one place because
///   the per-rule docs filename (<c>docs/rules/{id}_{symbolic}.md</c>), the descriptor
///   <c>HelpLinkUri</c> anchor, and the GitHub-case-sensitive source filename all have
///   to agree byte-for-byte for <c>--check</c> drift detection to mean anything.
/// </summary>
internal static class SymbolicNaming
{
    // Static fields rather than [GeneratedRegex] because the docs generator's containing
    // types are file/internal-scoped — the source generator can't extend them across files.
    // Compile-once caching here is functionally equivalent for a ~10-call-per-run path.
    private static readonly Regex SymbolicPrefix = new(@"^(?:QYL|Qyl|AL|Al)\d{4}", RegexOptions.Compiled);
    private static readonly Regex FileBasenamePrefix = new(@"^Qyl(\d{4})(.*)$", RegexOptions.Compiled);

    /// <summary>
    ///   Mirror of <c>AlAnalyzer.SymbolicNameFromFile</c> for the docs side. Strips the
    ///   <c>Analyzer</c> suffix and any <c>(QYL|Qyl|AL|Al)NNNN</c> prefix off the class
    ///   name; the remainder is the symbolic part used in per-rule docs filenames
    ///   <c>docs/rules/{id}_{symbolic}.md</c> and in the help-link URL. Keeping the
    ///   transform identical on both sides is what lets <c>--check</c> verify the
    ///   descriptor's <c>HelpLinkUri</c> against the file the generator would emit.
    /// </summary>
    public static string ToSymbolicName(string className)
    {
        var name = className;
        if (name.EndsWith("Analyzer", StringComparison.Ordinal))
            name = name[..^"Analyzer".Length];
        var prefix = SymbolicPrefix.Match(name);
        if (prefix.Success)
            name = name[prefix.Length..];
        return name;
    }

    /// <summary>
    ///   Maps an analyzer class name to its on-disk source filename. Reflected class
    ///   names use Pascal-case <c>Qyl</c> (e.g., <c>Qyl0006MissingSchemaUrlAnalyzer</c>),
    ///   but git tracks the files with uppercase <c>QYL</c> prefix
    ///   (<c>QYL0006MissingSchemaUrlAnalyzer.cs</c>). macOS's case-insensitive default
    ///   masks this locally; GitHub's case-sensitive URL space does not. This lifts
    ///   <c>Qyl{4-digit}</c> to <c>QYL{4-digit}</c> so per-rule Source links resolve
    ///   on GitHub. Non-prefixed names (e.g., <c>GraphqlDocumentOptInAnalyzer</c>) pass
    ///   through unchanged because the class name already matches the file name.
    /// </summary>
    public static string FileBasenameForClass(string className)
    {
        var m = FileBasenamePrefix.Match(className);
        return m.Success ? $"QYL{m.Groups[1].Value}{m.Groups[2].Value}" : className;
    }
}
