// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

extern alias analyzers;

using System.Text.RegularExpressions;
using RuleDocs = analyzers::Qyl.OpenTelemetry.SemanticConventions.Analyzers.RuleDocs;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   Rewrites the <c>Notes</c> column of
///   <c>src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/AnalyzerReleases.Shipped.md</c>
///   from free-form rule titles to the Microsoft pattern
///   <c>ClassName, [Documentation](url)</c>, where <c>ClassName</c> is the
///   <see cref="Microsoft.CodeAnalysis.Diagnostics.DiagnosticAnalyzer"/> subclass registering the id and <c>url</c> is
///   the <c>HelpLinkUri</c> anchor the descriptor itself uses. Multi-id analyzers
///   (e.g., the supplemental QYL0009/0010/0011 family) get the same class name in
///   every row, matching MS NetAnalyzers' rendering. RS2008 already enforces that
///   rows exist for every descriptor; this rewrite owns the Notes shape.
///
///   The class exposes the in-memory rewrite (<see cref="Rewrite"/>) separately from
///   the file-level <see cref="Run"/> driver so <see cref="DocsGenerator"/>'s
///   <c>--check</c> mode can compare rewritten-vs-existing without touching disk.
/// </summary>
internal static partial class ShippedNotesRewriter
{
    /// <summary>
    ///   Rewrites the Notes column in-memory. Preserves the input's line ending so a
    ///   CRLF-checked-in file stays CRLF and an LF-checked-in file stays LF — the file
    ///   layout is owned by whoever first wrote it, not by the rewriter.
    /// </summary>
    public static string Rewrite(string existing)
    {
        var idToClass = DescriptorCatalog.BuildIdToClassMap();
        var lineEnding = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split([lineEnding], StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            var m = RowRegex().Match(lines[i]);
            if (!m.Success) continue;
            var id = m.Groups[1].Value;
            if (!idToClass.TryGetValue(id, out var className)) continue;
            var category = m.Groups[2].Value.Trim();
            var severity = m.Groups[3].Value.Trim();
            var url = RuleDocs.HelpLink(id, SymbolicNaming.ToSymbolicName(className));
            lines[i] = $"{id} | {category} | {severity} | {className}, [Documentation]({url})";
        }
        return string.Join(lineEnding, lines);
    }

    public static int Run(string repoRoot)
    {
        var shippedPath = RepoLayout.ShippedReleasesPath(repoRoot);
        if (!File.Exists(shippedPath))
        {
            Console.Error.WriteLine($"Missing Shipped.md: {shippedPath}");
            return 1;
        }
        var existing = File.ReadAllText(shippedPath);
        var expected = Rewrite(existing);
        if (string.Equals(existing, expected, StringComparison.Ordinal))
        {
            Console.WriteLine($@"Shipped.md Notes already up to date: {Path.GetRelativePath(repoRoot, shippedPath)}");
            return 0;
        }
        File.WriteAllText(shippedPath, expected);
        Console.WriteLine($@"Rewrote Shipped.md Notes column: {Path.GetRelativePath(repoRoot, shippedPath)}");
        return 0;
    }

    [GeneratedRegex(@"^(QYL\d{4})\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*.*$")]
    private static partial Regex RowRegex();
}
