namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Emitters;

internal static class GeneratedSourceNames
{
    public static string ForPartialType(string containingNamespace, string className)
    {
        if (string.IsNullOrEmpty(containingNamespace))
            return className.SanitizeIdentifier() + ".g.cs";

        // Sanitize each namespace segment independently and rejoin with '.'. Naive
        // whole-string sanitization collapsed '.' → '_', so A.B and A_B (or
        // ConsumerB.Different.Nested.Path and ConsumerB_Different_Nested_Path)
        // produced colliding AddSource hint names.
        var segments = containingNamespace.Split('.');
        for (var i = 0; i < segments.Length; i++)
            segments[i] = segments[i].SanitizeIdentifier();

        return string.Join(".", segments) + "." + className.SanitizeIdentifier() + ".g.cs";
    }
}
