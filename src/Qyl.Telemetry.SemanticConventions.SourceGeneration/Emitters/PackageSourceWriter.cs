using System.Text;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Line-oriented writer for the compiled-package projection. Lines are joined with
/// <c>\n</c> regardless of host platform so the emitted files are byte-identical
/// everywhere; the projection's output is the package's shipped source.
/// </summary>
internal sealed class PackageSourceWriter
{
    private readonly List<string> _lines = new();

    public void Line(string text = "") => _lines.Add(text);

    public void DocLine(string pad, string line) =>
        _lines.Add(pad + "///" + (line.Length > 0 ? " " + line : string.Empty));

    public void Summary(string pad, IEnumerable<string> lines)
    {
        Raw(pad, "<summary>");
        foreach (var line in lines)
            DocLine(pad, line);
        Raw(pad, "</summary>");
    }

    public void Remarks(string pad, IEnumerable<string> lines)
    {
        Raw(pad, "<remarks>");
        foreach (var line in lines)
            DocLine(pad, line);
        Raw(pad, "</remarks>");
    }

    private void Raw(string pad, string tag) => _lines.Add(pad + "/// " + tag);

    public override string ToString()
    {
        var builder = new StringBuilder();
        foreach (var line in _lines)
            builder.Append(line).Append('\n');
        return builder.ToString();
    }

    /// <summary>A C# string literal: backslash, quote, CR and LF escaped.</summary>
    public static string CSharpString(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
}
