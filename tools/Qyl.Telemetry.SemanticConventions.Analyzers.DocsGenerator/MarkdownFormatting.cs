// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

using System.Text;

namespace Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   Markdown cell-escaping helpers shared by every renderer. Centralised so that all
///   tables emit the same escaping rules — descriptor titles/descriptions occasionally
///   contain pipes or newlines, and inconsistent escaping has bitten <c>--check</c>
///   drift in the past.
/// </summary>
internal static class MarkdownFormatting
{
    public static string Escape(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);

    public static string FormatReplacement(ImmutableArray<string> names) =>
        names.Length == 0
            ? "-"
            : Escape(string.Join(", ", names.Select(n => "`" + n + "`")));

    public static void WriteGeneratedFile(StringBuilder sb)
    {
        sb.AppendLine("## Generated File");
        sb.AppendLine();
        sb.AppendLine("Regenerate with:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("./build.sh GenerateDocs");
        sb.AppendLine("./build.sh AuditDocs    # prints catalog statistics, no file I/O");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Staleness is enforced automatically: every analyzer-project build fails if the committed markdown drifts from what the generator would emit.");
    }
}
