using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Byte-identity gate for the compiled-package projection: the assembly-level
/// <c>SemanticConventionAttributesPackage</c>, <c>SemanticConventionIncubatingAttributesPackage</c>
/// and <c>SemanticConventionTelemetryNamesPackage</c> markers must reproduce the files the
/// <c>Qyl.Telemetry.SemanticConventions</c> and <c>.Incubating</c> packages ship, byte for
/// byte. The snapshots were seeded from those shipped files.
/// </summary>
public sealed class PackageProjectionTests
{
    private const string StableSource = """
        using Qyl.Telemetry.SemanticConventions.SourceGeneration;
        [assembly: SemanticConventionAttributesPackage("Qyl.Telemetry.SemanticConventions")]
        """;

    private const string IncubatingSource = """
        using Qyl.Telemetry.SemanticConventions.SourceGeneration;
        [assembly: SemanticConventionIncubatingAttributesPackage("Qyl.Telemetry.SemanticConventions.Incubating")]
        [assembly: SemanticConventionTelemetryNamesPackage("Qyl.Telemetry.SemanticConventions.Incubating")]
        """;

    [Theory]
    [InlineData("Qyl.Telemetry.SemanticConventions.Attributes.Http.HttpAttributes.g.cs", "qyl.package.attributes.http.stable.expected.txt")]
    [InlineData("Qyl.Telemetry.SemanticConventions.SchemaUrl.g.cs", "qyl.package.schemaurl.expected.txt")]
    public void Stable_Package_Projection_Matches_Shipped_File(string hintName, string snapshotName)
    {
        var (result, output) = DefinitionsTestHost.Run<SemConvAttributesGenerator>(StableSource);

        var actual = result.GeneratedText(hintName);
        var expected = Snapshot.LoadOrRegen(actual, snapshotName);

        actual.Should().Be(expected, $"the stable package projection of '{hintName}' must be byte-identical to {snapshotName}");
        output.Errors().Should().BeEmpty();
    }

    [Theory]
    [InlineData("Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Http.HttpAttributes.g.cs", "qyl.package.attributes.http.incubating.expected.txt")]
    [InlineData("Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl.QylAttributes.g.cs", "qyl.package.attributes.qyl.incubating.expected.txt")]
    [InlineData("Qyl.Telemetry.SemanticConventions.Incubating.Names.QylTelemetryNames.g.cs", "qyl.package.names.expected.txt")]
    public void Incubating_Package_Projection_Matches_Shipped_File(string hintName, string snapshotName)
    {
        var (result, output) = DefinitionsTestHost.Run(
            IncubatingSource,
            referenceVocabulary: false,
            new SemConvAttributesGenerator(),
            new SemConvTelemetryNamesGenerator());

        var actual = result.GeneratedText(hintName);
        var expected = Snapshot.LoadOrRegen(actual, snapshotName);

        actual.Should().Be(expected, $"the incubating package projection of '{hintName}' must be byte-identical to {snapshotName}");
        output.Errors().Should().BeEmpty();
    }

    [Fact]
    public void Package_Projections_Cover_Every_Root_And_Compile_With_Well_Formed_Docs()
    {
        var (result, output) = DefinitionsTestHost.Run(
            StableSource + IncubatingSource.Replace("using Qyl.Telemetry.SemanticConventions.SourceGeneration;", string.Empty),
            referenceVocabulary: false,
            new SemConvAttributesGenerator(),
            new SemConvTelemetryNamesGenerator());

        var hintNames = result.GeneratedTrees.Select(static t => Path.GetFileName(t.FilePath)).ToList();
        hintNames.Should().OnlyHaveUniqueItems();

        var stableFiles = hintNames.Count(static n => n.StartsWith("Qyl.Telemetry.SemanticConventions.Attributes.", StringComparison.Ordinal));
        var incubatingFiles = hintNames.Count(static n => n.StartsWith("Qyl.Telemetry.SemanticConventions.Incubating.Attributes.", StringComparison.Ordinal));
        stableFiles.Should().BeGreaterThan(30, "every registry root with a stable row gets a class");
        incubatingFiles.Should().BeGreaterThan(stableFiles, "the incubating tier is a superset of roots");
        hintNames.Should().Contain("Qyl.Telemetry.SemanticConventions.SchemaUrl.g.cs")
            .And.Contain("Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl.QylAttributes.g.cs")
            .And.Contain("Qyl.Telemetry.SemanticConventions.Incubating.Names.QylTelemetryNames.g.cs")
            .And.NotContain("Qyl.Telemetry.SemanticConventions.Attributes.Qyl.QylAttributes.g.cs",
                "every qyl.* row is development-stability and therefore incubating-only");

        // Errors and CS157x doc-comment diagnostics both fail the gate: a malformed doc
        // comment in a shipped constant file would otherwise be dropped silently.
        var diagnostics = output.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error || d.Id.StartsWith("CS157", StringComparison.Ordinal))
            .ToList();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Package_Markers_Are_Assembly_Level_And_Take_A_Root_Namespace()
    {
        var (result, _) = DefinitionsTestHost.Run<SemConvAttributesGenerator>("namespace Empty;", referenceVocabulary: false);

        var marker = result.GeneratedText("SemanticConventionAttributesPackageAttribute.g.cs");
        marker.Should()
            .Contain("AttributeTargets.Assembly")
            .And.Contain("public SemanticConventionAttributesPackageAttribute(string rootNamespace)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }
}
