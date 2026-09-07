using System.Security.Cryptography;
using AwesomeAssertions;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// The byte-identity gate for the templates: <c>Snapshots/generated.manifest.sha256</c> pins the
/// SHA-256 of every file <c>scripts/generate.sh</c> writes, so an unintended change to a template
/// or to the registry names the exact files that moved.
/// </summary>
/// <remarks>
/// This test does not run Weaver; it compares the committed output to a pinned manifest, which
/// is deterministic on any machine. The other half of the gate — that the committed output is
/// what the pinned Weaver actually produces from <c>registry/</c> — is
/// <c>scripts/check-generated.sh</c>, which regenerates and runs <c>git diff --exit-code</c>;
/// CI runs it before the build. Regenerate this manifest with <c>REGEN_SNAPSHOTS=1</c> after an
/// intentional change.
/// </remarks>
public sealed class GeneratedSurfaceTests
{
    private static readonly string[] GeneratedRoots =
    {
        "src/Qyl.Telemetry.SemanticConventions/Generated",
        "src/Qyl.Telemetry.SemanticConventions.Incubating/Generated",
        "generated",
    };

    private static readonly string[] GeneratedFiles =
    {
        "src/Qyl.Telemetry.SemanticConventions.Analyzers/SemconvRegistryFacts.g.cs",
        "src/Qyl.Telemetry.SemanticConventions.Analyzers/SemconvDeprecations.g.cs",
    };

    [Fact]
    public void Every_Generated_File_Matches_The_Pinned_Manifest()
    {
        var manifest = string.Concat(Enumerate()
            .OrderBy(static entry => entry.Path, StringComparer.Ordinal)
            .Select(static entry => $"{entry.Hash}  {entry.Path}\n"));

        var snapshot = RepoPath("tests", "Qyl.Telemetry.SemanticConventions.Pipeline.Tests",
            "Snapshots", "generated.manifest.sha256");

        if (Environment.GetEnvironmentVariable("REGEN_SNAPSHOTS") is { Length: > 0 })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.WriteAllText(snapshot, manifest);
        }

        File.Exists(snapshot).Should().BeTrue($"the pinned manifest {snapshot} must be committed");

        var expected = Parse(File.ReadAllText(snapshot));
        var actual = Parse(manifest);

        var problems = new List<string>();
        foreach (var (path, hash) in expected.OrderBy(static e => e.Key, StringComparer.Ordinal))
        {
            if (!actual.TryGetValue(path, out var actualHash))
                problems.Add($"missing: {path}");
            else if (!string.Equals(actualHash, hash, StringComparison.Ordinal))
                problems.Add($"differing: {path}");
        }

        foreach (var path in actual.Keys.Where(path => !expected.ContainsKey(path)).Order(StringComparer.Ordinal))
            problems.Add($"extra: {path}");

        expected.Should().NotBeEmpty("the manifest pins the full generated surface");
        problems.Should().BeEmpty(
            "every generated file must match generated.manifest.sha256 (regenerate with REGEN_SNAPSHOTS after an intentional change):\n{0}",
            string.Join("\n", problems));
        actual.Count.Should().Be(expected.Count);
    }

    [Fact]
    public void The_Generated_Surface_Covers_Both_Packages_And_Every_Signal_Kind()
    {
        var paths = Enumerate().Select(static entry => entry.Path).ToList();

        foreach (var kind in new[] { "Attributes", "Activities", "Metrics", "Spans", "Events", "Entities" })
        {
            paths.Should().Contain(path => path.Contains($"/Generated/{kind}/", StringComparison.Ordinal)
                                           && path.StartsWith("src/Qyl.Telemetry.SemanticConventions/", StringComparison.Ordinal),
                $"the stable package ships {kind}");
            paths.Should().Contain(path => path.Contains($"/Generated/{kind}/", StringComparison.Ordinal)
                                           && path.StartsWith("src/Qyl.Telemetry.SemanticConventions.Incubating/", StringComparison.Ordinal),
                $"the incubating package ships {kind}");
        }

        paths.Should()
            .Contain("src/Qyl.Telemetry.SemanticConventions/Generated/SchemaUrl.g.cs")
            .And.Contain("src/Qyl.Telemetry.SemanticConventions/Generated/Names/QylTelemetryNames.g.cs")
            .And.Contain("src/Qyl.Telemetry.SemanticConventions.Incubating/Generated/Mapping/AttributeMapping.g.cs")
            .And.Contain("generated/typespec/otel-keys.gen.tsp")
            .And.Contain("generated/pins.props");
    }

    private static IEnumerable<(string Path, string Hash)> Enumerate()
    {
        foreach (var root in GeneratedRoots)
        {
            var directory = RepoPath(root.Split('/'));
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                yield return Entry(file);
        }

        foreach (var file in GeneratedFiles)
            yield return Entry(RepoPath(file.Split('/')));
    }

    private static (string Path, string Hash) Entry(string absolute)
    {
        var relative = Path.GetRelativePath(RepoPath(), absolute).Replace(Path.DirectorySeparatorChar, '/');
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(absolute))).ToLowerInvariant();
        return (relative, hash);
    }

    private static Dictionary<string, string> Parse(string text)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf("  ", StringComparison.Ordinal);
            separator.Should().BeGreaterThan(0, $"manifest line '{line}' must be '<sha256>  <path>'");
            entries.Add(line.Substring(separator + 2), line.Substring(0, separator));
        }
        return entries;
    }

    private static string RepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Qyl.Telemetry.SemanticConventions.slnx")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test must run inside the repository");
        return Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
    }
}
