namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Embedded snapshot access shared by the byte-identity gates. Snapshots are updated only
/// through <c>REGEN_SNAPSHOTS</c>: when that environment variable names the absolute
/// <c>Snapshots/</c> directory, the actual output is written there and returned, so the
/// caller's equality assertion passes trivially and the diff lands in the working tree for
/// review.
/// </summary>
internal static class Snapshot
{
    public static string LoadOrRegen(string actual, string resourceName)
    {
        var regenRoot = Environment.GetEnvironmentVariable("REGEN_SNAPSHOTS");
        if (!string.IsNullOrEmpty(regenRoot))
        {
            File.WriteAllText(Path.Combine(regenRoot, resourceName), actual);
            return actual;
        }
        return Load(resourceName);
    }

    public static string Load(string resourceName)
    {
        var assembly = typeof(Snapshot).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Snapshot resource '{resourceName}' was not embedded into {assembly.FullName}. " +
                "Check the EmbeddedResource entry in Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
