using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class span/event surfaces:
/// <c>[SemanticConventionSpanDefinitions]</c> emits typed <c>SpanDefinition&lt;TKind&gt;</c>
/// and <c>[SemanticConventionEventDefinitions]</c> emits <c>EventDefinition</c> fields, both
/// bound to the vocabulary package's definition types.
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

        var (result, output) = DefinitionsTestHost.Run<SemConvSpanDefinitionsGenerator>(source);
        var generated = result.GeneratedText("HttpSpanDefinitions.g.cs");

        // http.client is a client span; the kind is a marker type.
        generated.Should()
            .Contain("SpanDefinition<global::Qyl.Telemetry.SemanticConventions.Client> HttpClient")
            .And.Contain("id: \"http.client\"")
            .And.Contain("SpanDefinition<global::Qyl.Telemetry.SemanticConventions.Server> HttpServer");

        output.Errors().Should().BeEmpty();
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

        var (result, output) = DefinitionsTestHost.Run<SemConvEventDefinitionsGenerator>(source);
        var generated = result.GeneratedText("AppEventDefinitions.g.cs");

        // app.crash is an event.
        generated.Should()
            .Contain("global::Qyl.Telemetry.SemanticConventions.EventDefinition AppCrash")
            .And.Contain("name: \"app.crash\"");

        output.Errors().Should().BeEmpty();
    }

    [Fact]
    public void Span_And_Event_Surfaces_Report_QYLSG001_Without_The_Vocabulary_Package()
    {
        const string spanSource = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionSpanDefinitions("http")]
            internal static partial class HttpSpanDefinitions;
            """;
        const string eventSource = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionEventDefinitions("app")]
            internal static partial class AppEventDefinitions;
            """;

        var (spans, _) = DefinitionsTestHost.Run<SemConvSpanDefinitionsGenerator>(spanSource, referenceVocabulary: false);
        var (events, _) = DefinitionsTestHost.Run<SemConvEventDefinitionsGenerator>(eventSource, referenceVocabulary: false);

        spans.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("QYLSG001");
        spans.Diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("[SemanticConventionSpanDefinitions]");
        events.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("QYLSG001");
        events.Diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("[SemanticConventionEventDefinitions]");
    }
}
