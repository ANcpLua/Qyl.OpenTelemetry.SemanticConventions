// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator;

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
}
