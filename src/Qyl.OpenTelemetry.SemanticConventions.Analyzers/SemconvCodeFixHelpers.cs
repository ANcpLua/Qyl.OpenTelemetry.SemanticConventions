// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

internal static class SemconvCodeFixHelpers
{
    public const string ReplacementValueProperty = "ReplacementValue";

    public static bool TryExtractExactReplacement(
        string message,
        [NotNullWhen(true)] out string? replacement)
    {
        replacement = null;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (TryExtractCodeTagReplacement(message, out replacement))
        {
            return true;
        }

        const string prefix = "Replaced by ";
        var trimmed = message.Trim();
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        replacement = NormalizeCandidate(trimmed.Substring(prefix.Length));
        return IsExactReplacementCandidate(replacement);
    }

    public static LiteralExpressionSyntax CreateReplacementLiteral(
        LiteralExpressionSyntax original,
        string replacement)
    {
        var token = original.Token;
        var text = token.Text;
        var literalText = text.StartsWith("@\"", StringComparison.Ordinal)
            ? "@\"" + replacement.Replace("\"", "\"\"") + "\""
            : "\"" + EscapeStringLiteral(replacement) + "\"";

        return SyntaxFactory.LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(token.LeadingTrivia, literalText, replacement, token.TrailingTrivia))
            .WithTriviaFrom(original);
    }

    private static bool TryExtractCodeTagReplacement(
        string message,
        [NotNullWhen(true)] out string? replacement)
    {
        replacement = null;
        var start = message.IndexOf("<c>", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += "<c>".Length;
        var end = message.IndexOf("</c>", start, StringComparison.Ordinal);
        if (end <= start)
        {
            return false;
        }

        replacement = NormalizeCandidate(message.Substring(start, end - start));
        return IsExactReplacementCandidate(replacement);
    }

    private static string NormalizeCandidate(string value)
    {
        var candidate = value.Trim().TrimEnd('.').Trim();
        candidate = candidate.Trim('`', '"', '\'');
        return candidate;
    }

    private static bool IsExactReplacementCandidate([NotNullWhen(true)] string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value!;
        foreach (var ch in candidate)
        {
            if (char.IsWhiteSpace(ch)
                || ch == ','
                || ch == ';'
                || ch == '('
                || ch == ')')
            {
                return false;
            }
        }

        return true;
    }

    private static string EscapeStringLiteral(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    builder.Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
