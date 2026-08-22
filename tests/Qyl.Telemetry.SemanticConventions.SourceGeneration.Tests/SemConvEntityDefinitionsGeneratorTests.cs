using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class entity surface:
/// <c>[SemanticConventionEntityDefinitions]</c> emits <c>EntityDefinition</c> objects
/// carrying the entity's describing/identifying attribute references.
/// </summary>
public sealed class SemConvEntityDefinitionsGeneratorTests
{
    [Fact]
    public void Emits_EntityDefinitions_For_Host_Marker()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionIncubatingEntityDefinitions("host")]
            internal static partial class HostEntityDefinitions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEntityDefinitionsGenerator>(source);
        var generated = string.Concat(result.RunResult.GeneratedTrees
            .Where(static t => t.FilePath.Contains("HostEntityDefinitions", StringComparison.Ordinal))
            .Select(static t => t.ToString()));

        // The host entity carries its identifying/describing attributes (host.arch, host.id, ...).
        generated.Should()
            .Contain("global::Qyl.Telemetry.SemanticConventions.EntityDefinition Host")
            .And.Contain("name: \"host\"")
            .And.Contain("new(\"host.arch\"")
            .And.Contain("new(\"host.id\"");
    }

    [Fact]
    public void Support_Types_Include_Entity_Definition()
    {
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);
        var support = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MetricDefinition.Support.g.cs", StringComparison.Ordinal))
            .ToString();

        support.Should()
            .Contain("public sealed class EntityDefinition")
            .And.Contain("The attributes that describe and identify this entity.");
    }
}
