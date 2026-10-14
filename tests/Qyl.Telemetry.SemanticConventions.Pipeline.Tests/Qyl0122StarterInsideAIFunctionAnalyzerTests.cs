using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>QYL0122: FunctionInvokingChatClient already records the execute_tool span of an AIFunction (ADR D6).</summary>
public sealed class Qyl0122StarterInsideAIFunctionAnalyzerTests
{
    private static readonly (string, string) MicrosoftExtensionsAI = ("Microsoft.Extensions.AI", """
        namespace Microsoft.Extensions.AI
        {
            public static class AIFunctionFactory
            {
                public static object Create(System.Delegate method, string? name = null) => method;
            }
        }
        """);

    [Fact]
    public async Task Reports_a_starter_inside_the_function_delegate()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0122StarterInsideAIFunctionAnalyzer(),
            "QYL0122",
            """
            using Microsoft.Extensions.AI;
            using Qyl;

            public static class Tools
            {
                public static object LookupStock() => AIFunctionFactory.Create((string sku) =>
                {
                    using var span = IncubatingSpans.GenAiExecuteTool("lookup_stock", agentName: null, conversationId: null);
                    using var work = Spans.Work("lookup stock");
                    return 3;
                }, "lookup_stock");
            }
            """,
            libraries: [MicrosoftExtensionsAI]);

        diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public async Task Is_silent_outside_the_function_delegate()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0122StarterInsideAIFunctionAnalyzer(),
            "QYL0122",
            """
            using Microsoft.Extensions.AI;
            using Qyl;

            public static class Tools
            {
                public static object LookupStock()
                {
                    using var span = Spans.Work("register tools");
                    return AIFunctionFactory.Create((string sku) => 3, "lookup_stock");
                }
            }
            """,
            libraries: [MicrosoftExtensionsAI]);

        diagnostics.Should().BeEmpty();
    }
}
