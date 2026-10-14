using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>QYL0121: Custom is the escape hatch for a value the registry does not list, not a second spelling of a member.</summary>
public sealed class Qyl0121CustomValueEqualsMemberAnalyzerTests
{
    [Fact]
    public async Task Reports_Custom_with_a_value_a_member_carries()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0121CustomValueEqualsMemberAnalyzer(),
            "QYL0121",
            """
            using Qyl.Telemetry.SemanticConventions.Activities;
            using Qyl.Telemetry.SemanticConventions.Incubating.Activities;

            public static class Values
            {
                public static readonly DbSystemName Postgres = DbSystemName.Custom("postgresql");
                public static readonly MessagingSystem Kafka = MessagingSystem.Custom("kafka");
            }
            """);

        diagnostics.Should().HaveCount(2);
        diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Should()
            .Contain(static m => m.Contains("DbSystemName.Postgresql", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("MessagingSystem.Kafka", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Is_silent_for_a_value_the_registry_does_not_list()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0121CustomValueEqualsMemberAnalyzer(),
            "QYL0121",
            """
            using Qyl.Telemetry.SemanticConventions.Activities;

            public static class Values
            {
                public static DbSystemName Memory => DbSystemName.Custom("memory");
                public static DbSystemName FromConfig(string value) => DbSystemName.Custom(value);
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
