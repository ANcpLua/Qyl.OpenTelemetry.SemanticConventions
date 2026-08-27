using System.Text;
using System.Text.RegularExpressions;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Markdown-to-XML-doc conversion for the compiled-package projection. Registry
/// <c>brief</c>/<c>note</c> fields are GitHub-flavoured markdown; they are rendered into
/// C# XML doc comments, which the compiler parses as XML, so the output must be
/// well-formed (a mismatched tag makes the compiler drop the whole comment). Every
/// construct therefore emits balanced or self-closing XML only:
/// <list type="bullet">
///   <item>paragraph break → a self-closing <c>&lt;para/&gt;</c> separator line</item>
///   <item>inline code → <c>&lt;c&gt;</c></item>
///   <item>fenced code → a <c>&lt;code&gt;</c> block</item>
///   <item>link → <c>&lt;a href&gt;</c> with balanced-parenthesis URL parsing; image → alt text only</item>
///   <item>GitHub alert <c>[!WARNING]</c> → <c>Warning:</c></item>
///   <item>blockquote → markers stripped; a leading quote block is wrapped in <c>&lt;blockquote&gt;</c></item>
///   <item>GFM table separator row → dropped</item>
/// </list>
/// All literal text is XML-escaped. The rules are the package projection's contract: the
/// shipped constant files are byte-identical to what they produce.
/// </summary>
internal static class PackageDocComments
{
    private static readonly Regex s_alert = new(@"\G\[!(\w+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex s_tableSeparator = new(@"^\s*\|?[\s:|-]*-[\s:|-]*\|?\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex s_blockquoteMarker = new(@"^\s*>\s?", RegexOptions.CultureInvariant);
    private static readonly Regex s_whitespaceRun = new(@"\s+", RegexOptions.CultureInvariant);

    public static string Escape(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string CollapseWhitespace(string text) =>
        s_whitespaceRun.Replace(text, " ").Trim();

    /// <summary>Converts inline markdown (which may span newlines) to escaped XML-doc markup.</summary>
    public static string RenderInline(string text)
    {
        var output = new StringBuilder(text.Length + 16);
        var i = 0;
        var n = text.Length;
        while (i < n)
        {
            var ch = text[i];
            if (ch == '`')
            {
                var j = i + 1;
                while (j < n && text[j] == '`')
                    j++;
                var ticks = text.Substring(i, j - i);
                var close = text.IndexOf(ticks, j, StringComparison.Ordinal);
                if (close < 0)
                {
                    output.Append(Escape(ch.ToString()));
                    i++;
                    continue;
                }

                output.Append("<c>").Append(Escape(text.Substring(j, close - j))).Append("</c>");
                i = close + ticks.Length;
                continue;
            }

            if (ch == '!' && i + 1 < n && text[i + 1] == '[')
            {
                if (TryParseLink(text, i + 1, out var alt, out _, out var end))
                {
                    output.Append(Escape(CollapseWhitespace(alt)));
                    i = end;
                    continue;
                }

                output.Append('!');
                i++;
                continue;
            }

            if (ch == '[')
            {
                var alert = s_alert.Match(text, i);
                if (alert.Success)
                {
                    output.Append(Escape(Capitalize(alert.Groups[1].Value) + ":"));
                    i += alert.Length;
                    continue;
                }

                if (TryParseLink(text, i, out var linkText, out var url, out var end))
                {
                    output.Append("<a href=\"").Append(Escape(url.Trim())).Append("\">")
                          .Append(Escape(CollapseWhitespace(linkText))).Append("</a>");
                    i = end;
                    continue;
                }

                output.Append(Escape(ch.ToString()));
                i++;
                continue;
            }

            output.Append(Escape(ch.ToString()));
            i++;
        }

        return output.ToString();
    }

    /// <summary>
    /// Converts a markdown brief/note into well-formed XML-doc content lines. Paragraphs are
    /// separated by a self-closing <c>&lt;para/&gt;</c> line.
    /// </summary>
    public static List<string> RenderMarkdown(string text)
    {
        text = text.TrimEnd('\n');
        var output = new List<string>();
        if (text.Length == 0)
            return output;

        var first = true;
        foreach (var (isCode, lines) in SegmentBlocks(text))
        {
            List<string> blockLines;
            if (isCode)
            {
                blockLines = new List<string>(lines.Count + 2) { "<code>" };
                foreach (var line in lines)
                    blockLines.Add(Escape(line));
                blockLines.Add("</code>");
            }
            else
            {
                var kept = new List<string>(lines.Count);
                foreach (var line in lines)
                {
                    if (!s_tableSeparator.IsMatch(line))
                        kept.Add(line);
                }
                if (kept.Count == 0)
                    continue;

                var isBlockquote = true;
                var anyNonEmpty = false;
                foreach (var line in kept)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    anyNonEmpty = true;
                    if (!line.TrimStart().StartsWith(">", StringComparison.Ordinal))
                    {
                        isBlockquote = false;
                        break;
                    }
                }
                isBlockquote &= anyNonEmpty;

                if (isBlockquote)
                {
                    var stripped = new List<string>(kept.Count);
                    foreach (var line in kept)
                        stripped.Add(s_blockquoteMarker.Replace(line, string.Empty, 1));
                    var rendered = new List<string>(RenderInline(string.Join("\n", stripped)).Split('\n'));
                    if (first)
                    {
                        rendered.Insert(0, "<blockquote>");
                        rendered[rendered.Count - 1] += "</blockquote>";
                    }
                    blockLines = rendered;
                }
                else
                {
                    blockLines = new List<string>(RenderInline(string.Join("\n", kept)).Split('\n'));
                }
            }

            if (!first)
                output.Add("<para/>");
            output.AddRange(blockLines);
            first = false;
        }

        return output;
    }

    /// <summary>
    /// Splits note text into text and fenced-code blocks. Fenced blocks are atomic; other
    /// blocks are separated by blank lines.
    /// </summary>
    private static List<(bool IsCode, List<string> Lines)> SegmentBlocks(string text)
    {
        var blocks = new List<(bool, List<string>)>();
        var buffer = new List<string>();
        var fence = new List<string>();
        var inFence = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.Trim().StartsWith("```", StringComparison.Ordinal))
            {
                if (!inFence)
                {
                    if (buffer.Count > 0)
                    {
                        blocks.Add((false, buffer));
                        buffer = new List<string>();
                    }
                    inFence = true;
                    fence = new List<string>();
                }
                else
                {
                    blocks.Add((true, fence));
                    fence = new List<string>();
                    inFence = false;
                }
                continue;
            }

            if (inFence)
            {
                fence.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (buffer.Count > 0)
                {
                    blocks.Add((false, buffer));
                    buffer = new List<string>();
                }
                continue;
            }

            buffer.Add(line);
        }

        if (inFence && fence.Count > 0)
            blocks.Add((true, fence));
        if (buffer.Count > 0)
            blocks.Add((false, buffer));
        return blocks;
    }

    /// <summary>
    /// Parses a markdown link or image body starting at the <c>[</c> at <paramref name="start"/>.
    /// Bracketed text may nest and span newlines; the URL is read with balanced parentheses
    /// so inner or trailing <c>)</c> stay in the href instead of truncating the link.
    /// </summary>
    private static bool TryParseLink(string text, int start, out string linkText, out string url, out int end)
    {
        linkText = string.Empty;
        url = string.Empty;
        end = start;
        var n = text.Length;
        if (start >= n || text[start] != '[')
            return false;

        var i = start + 1;
        var depth = 1;
        var textBuilder = new StringBuilder();
        var closed = false;
        while (i < n)
        {
            var c = text[i];
            if (c == '[')
            {
                depth++;
                textBuilder.Append(c);
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    i++;
                    closed = true;
                    break;
                }
                textBuilder.Append(c);
            }
            else
            {
                textBuilder.Append(c);
            }
            i++;
        }
        if (!closed)
            return false;

        if (i >= n || text[i] != '(')
            return false;
        i++;
        var parenDepth = 1;
        var urlBuilder = new StringBuilder();
        closed = false;
        while (i < n)
        {
            var c = text[i];
            if (c == '(')
            {
                parenDepth++;
                urlBuilder.Append(c);
            }
            else if (c == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                {
                    i++;
                    closed = true;
                    break;
                }
                urlBuilder.Append(c);
            }
            else
            {
                urlBuilder.Append(c);
            }
            i++;
        }
        if (!closed)
            return false;

        linkText = textBuilder.ToString();
        url = urlBuilder.ToString();
        end = i;
        return true;
    }

    /// <summary>First character upper-cased, the rest lower-cased (the alert word, e.g. <c>Warning</c>).</summary>
    private static string Capitalize(string word) =>
        word.Length == 0
            ? word
            : char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
}
