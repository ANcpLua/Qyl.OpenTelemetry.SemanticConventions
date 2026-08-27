using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class entity surface:
/// <c>[SemanticConventionEntityDefinitions]</c> emits <c>EntityDefinition</c> fields
/// carrying the entity's describing/identifying attribute references, bound to the
/// vocabulary package's definition types.
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

        var (result, output) = DefinitionsTestHost.Run<SemConvEntityDefinitionsGenerator>(source);
        var generated = result.GeneratedText("HostEntityDefinitions.g.cs");

        // The host entity carries its identifying/describing attributes (host.arch, host.id, ...).
        generated.Should()
            .Contain("global::Qyl.Telemetry.SemanticConventions.EntityDefinition Host")
            .And.Contain("name: \"host\"")
            .And.Contain("new(\"host.arch\"")
            .And.Contain("new(\"host.id\"");

        output.Errors().Should().BeEmpty();
    }

    [Fact]
    public void Entity_Surface_Reports_QYLSG001_Without_The_Vocabulary_Package()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionEntityDefinitions("host")]
            internal static partial class HostEntityDefinitions;
            """;

        var (result, _) = DefinitionsTestHost.Run<SemConvEntityDefinitionsGenerator>(source, referenceVocabulary: false);

        result.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("QYLSG001");
        result.Diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("[SemanticConventionEntityDefinitions]");
    }
}
