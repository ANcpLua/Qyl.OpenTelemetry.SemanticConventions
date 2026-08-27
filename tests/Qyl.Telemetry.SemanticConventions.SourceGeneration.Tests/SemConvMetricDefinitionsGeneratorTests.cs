using System.Globalization;
using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class metric-definition surface:
/// <c>[SemanticConventionMetricDefinitions("&lt;prefix&gt;")]</c> emits typed
/// <c>MetricDefinition&lt;TInstrument&gt;</c> fields bound to the vocabulary package's
/// definition types.
/// </summary>
public sealed class SemConvMetricDefinitionsGeneratorTests
{
    [Fact]
    public void Emits_MetricDefinitionsMarker_Attribute_PostInitialization()
    {
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionMetricDefinitionsAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.Telemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionMetricDefinitionsAttribute")
            .And.Contain("public SemanticConventionMetricDefinitionsAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Does_Not_Emit_Definition_Types()
    {
        // The definition types have exactly one home: the Qyl.Telemetry.SemanticConventions
        // package. The generator names them textually and never re-emits them per consumer.
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);

        result.RunResult.GeneratedTrees
            .Select(static t => Path.GetFileName(t.FilePath))
            .Should().NotContain("MetricDefinition.Support.g.cs");
        result.RunResult.GeneratedTrees
            .Select(static t => t.ToString())
            .Should().NotContain(static text => text.Contains("class MetricDefinition<", StringComparison.Ordinal));
    }

    [Fact]
    public void Reports_QYLSG001_When_Vocabulary_Package_Is_Not_Referenced()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMetricDefinitions("process")]
            internal static partial class ProcessMetricDefinitions;
            """;

        var (result, _) = DefinitionsTestHost.Run<SemConvMetricDefinitionsGenerator>(source, referenceVocabulary: false);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("QYLSG001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).Should()
            .Contain("'ProcessMetricDefinitions'")
            .And.Contain("[SemanticConventionIncubatingMetricDefinitions]")
            .And.Contain("Qyl.Telemetry.SemanticConventions");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.Should().Be(5,
            "the diagnostic points at the marked class declaration");

        result.GeneratedTrees
            .Select(static t => Path.GetFileName(t.FilePath))
            .Should().NotContain("MyApp.ProcessMetricDefinitions.g.cs",
                "a marker whose compilation lacks the vocabulary package produces the diagnostic, not unresolvable source");
    }

    [Fact]
    public void Emits_FirstClass_Definitions_For_Process_Marker()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMetricDefinitions("process")]
            internal static partial class ProcessMetricDefinitions;
            """;

        var (result, output) = DefinitionsTestHost.Run<SemConvMetricDefinitionsGenerator>(source);

        result.Diagnostics.Should().BeEmpty();
        var generated = result.GeneratedText("ProcessMetricDefinitions.g.cs");

        // process.disk.operations is a Counter with unit {operation}, entity=process,
        // and a required disk.io.direction attribute — all first-class on the object.
        generated.Should()
            .Contain("MetricDefinition<global::Qyl.Telemetry.SemanticConventions.Counter> ProcessDiskOperations")
            .And.Contain("name: \"process.disk.operations\"")
            .And.Contain("unit: \"{operation}\"")
            .And.Contain("entities: new global::Qyl.Telemetry.SemanticConventions.EntityRef[] { new(\"process\") }")
            .And.Contain("new(\"disk.io.direction\", \"enum\", global::Qyl.Telemetry.SemanticConventions.RequirementLevel.Required, false)");

        // process.memory.utilization is a Gauge.
        generated.Should()
            .Contain("MetricDefinition<global::Qyl.Telemetry.SemanticConventions.Gauge> ProcessMemoryUtilization");

        // system.process.limit is NOT under the "process" prefix (it's system.*) — must be absent.
        generated.Should().NotContain("system.process.limit");

        output.Errors().Should().BeEmpty("the generated definitions must bind to the vocabulary package's types");
    }

    [Fact]
    public void Instrument_Kind_Resolves_From_The_Marker_Type()
    {
        // The netstandard2.0-compatible marker design: the kind is an instance property on a
        // stateless struct, read through default(TInstrument) — no static abstract members.
        new MetricDefinition<Counter>("m", "1", "b", Stability.Stable, Deprecation.None, [], [])
            .Instrument.Should().Be("counter");
        new MetricDefinition<UpDownCounter>("m", "1", "b", Stability.Stable, Deprecation.None, [], [])
            .Instrument.Should().Be("updowncounter");
        new MetricDefinition<Gauge>("m", "1", "b", Stability.Stable, Deprecation.None, [], [])
            .Instrument.Should().Be("gauge");
        new MetricDefinition<Histogram>("m", "1", "b", Stability.Stable, Deprecation.None, [], [])
            .Instrument.Should().Be("histogram");

        new SpanDefinition<Client>("s", "b", Stability.Stable, Deprecation.None, []).SpanKind.Should().Be("client");
        new SpanDefinition<Server>("s", "b", Stability.Stable, Deprecation.None, []).SpanKind.Should().Be("server");
        new SpanDefinition<Internal>("s", "b", Stability.Stable, Deprecation.None, []).SpanKind.Should().Be("internal");
        new SpanDefinition<Producer>("s", "b", Stability.Stable, Deprecation.None, []).SpanKind.Should().Be("producer");
        new SpanDefinition<Consumer>("s", "b", Stability.Stable, Deprecation.None, []).SpanKind.Should().Be("consumer");
    }
}
