using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class span/event surfaces:
/// <c>[SemanticConventionSpanDefinitions]</c> emits typed <c>SpanDefinition&lt;TKind&gt;</c>
/// and <c>[SemanticConventionEventDefinitions]</c> emits <c>EventDefinition</c> objects.
/// </summary>
public sealed class SemConvSpanEventDefinitionsGeneratorTests
{
    [Fact]
    public void Emits_SpanDefinitions_For_Http_Marker()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionIncubatingSpanDefinitions("http")]
            internal static partial class HttpSpanDefinitions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvSpanDefinitionsGenerator>(source);
        var generated = string.Concat(result.RunResult.GeneratedTrees
            .Where(static t => t.FilePath.Contains("HttpSpanDefinitions", StringComparison.Ordinal))
            .Select(static t => t.ToString()));

        // http.client is a client span; the kind is a marker type.
        generated.Should()
            .Contain("SpanDefinition<global::Qyl.Telemetry.SemanticConventions.Client> HttpClient")
            .And.Contain("id: \"http.client\"")
            .And.Contain("SpanDefinition<global::Qyl.Telemetry.SemanticConventions.Server> HttpServer");
    }

    [Fact]
    public void Emits_EventDefinitions_For_App_Marker()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionIncubatingEventDefinitions("app")]
            internal static partial class AppEventDefinitions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventDefinitionsGenerator>(source);
        var generated = string.Concat(result.RunResult.GeneratedTrees
            .Where(static t => t.FilePath.Contains("AppEventDefinitions", StringComparison.Ordinal))
            .Select(static t => t.ToString()));

        // app.crash is an event.
        generated.Should()
            .Contain("global::Qyl.Telemetry.SemanticConventions.EventDefinition AppCrash")
            .And.Contain("name: \"app.crash\"");
    }

    [Fact]
    public void Support_Types_Include_Span_And_Event_Definitions()
    {
        const string source = "namespace Empty;";

        // Support types are emitted by the metric-definitions generator's post-init.
        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);
        var support = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MetricDefinition.Support.g.cs", StringComparison.Ordinal))
            .ToString();

        support.Should()
            .Contain("public sealed class SpanDefinition<TKind> where TKind : struct, ISpanKind")
            .And.Contain("public sealed class EventDefinition")
            .And.Contain("public readonly struct Client : ISpanKind")
            .And.Contain("public readonly struct Server : ISpanKind")
            .And.Contain("public string SpanKind => TKind.Kind;");
    }
}
