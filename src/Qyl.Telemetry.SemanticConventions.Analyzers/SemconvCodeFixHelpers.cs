// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

internal static class SemconvCodeFixHelpers
{
    public const string ReplacementValueProperty = "ReplacementValue";

    /// <summary>Fallback when an <c>[Obsolete]</c> attribute carries no usable message.</summary>
    public const string MissingObsoleteMessage = "no replacement message provided";

    /// <summary>The <c>[Obsolete]</c> message, or <see cref="MissingObsoleteMessage"/> when absent or empty.</summary>
    public static string GetObsoleteMessage(AttributeData obsolete) =>
        obsolete.TryGetConstructorArgument<string>(0, out var message) && !string.IsNullOrEmpty(message)
            ? message
            : MissingObsoleteMessage;

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

        if (TryExtractQuotedUseReplacement(message, out replacement))
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

    /// <summary>
    /// Finds an attribute on <paramref name="node"/> by short name, tolerating namespace
    /// qualification and the <c>Attribute</c> suffix.
    /// </summary>
    public static AttributeSyntax? FindAttributeByName(SyntaxNode node, string attributeShortName)
    {
        foreach (var attributeList in node.ChildNodes().OfType<AttributeListSyntax>())
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name == attributeShortName || name.EndsWithOrdinal("." + attributeShortName) ||
                    name == attributeShortName + "Attribute" ||
                    name.EndsWithOrdinal("." + attributeShortName + "Attribute"))
                {
                    return attribute;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the attribute — or its whole list when it is the only entry — and
    /// returns the updated document.
    /// </summary>
    public static Document RemoveAttribute(Document document, SyntaxNode root, AttributeSyntax attribute)
    {
        if (attribute.Parent is not AttributeListSyntax attributeList)
        {
            return document;
        }

        var newRoot = attributeList.Attributes.Count == 1
            ? root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia)!
            : root.ReplaceNode(attributeList, attributeList.WithAttributes(attributeList.Attributes.Remove(attribute)));

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool TryExtractQuotedUseReplacement(string guidance, [NotNullWhen(true)] out string? replacement)
    {
        const string quotedPrefix = "Use '";
        if (guidance.StartsWith(quotedPrefix, StringComparison.Ordinal))
        {
            var start = quotedPrefix.Length;
            var end = guidance.IndexOf('\'', start);
            if (end > start)
            {
                replacement = guidance.Substring(start, end - start);
                return true;
            }
        }

        const string backtickPrefix = "Use `";
        if (guidance.StartsWith(backtickPrefix, StringComparison.Ordinal))
        {
            var start = backtickPrefix.Length;
            var end = guidance.IndexOf('`', start);
            if (end > start)
            {
                replacement = guidance.Substring(start, end - start);
                return true;
            }
        }

        replacement = null;
        return false;
    }

    public static LiteralExpressionSyntax CreateReplacementLiteral(
        LiteralExpressionSyntax original,
        string replacement)
    {
        var token = original.Token;
        var text = token.Text;
        var literalText = text.StartsWith("@\"", StringComparison.Ordinal)
            ? "@\"" + replacement.Replace("\"", "\"\"") + "\""
            : "\"" + replacement.EscapeCSharpString() + "\"";

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
}
