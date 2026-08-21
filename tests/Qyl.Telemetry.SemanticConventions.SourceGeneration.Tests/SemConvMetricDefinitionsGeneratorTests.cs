using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Tests the first-class metric-definition surface:
/// <c>[SemanticConventionMetricDefinitions("&lt;prefix&gt;")]</c> emits typed
/// <c>MetricDefinition&lt;TInstrument&gt;</c> objects and the shared runtime support types.
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
    public void Emits_Support_Types_PostInitialization()
    {
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);

        var support = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("MetricDefinition.Support.g.cs", StringComparison.Ordinal))
            .ToString();

        support.Should()
            .Contain("namespace Qyl.Telemetry.SemanticConventions;")
            .And.Contain("public sealed class MetricDefinition<TInstrument> where TInstrument : struct, IInstrument")
            .And.Contain("public readonly struct Counter : IInstrument")
            .And.Contain("public readonly struct Gauge : IInstrument")
            .And.Contain("public readonly struct UpDownCounter : IInstrument")
            .And.Contain("public static Deprecation Renamed(string replacement)")
            .And.Contain("public string Instrument => TInstrument.Kind;");
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

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricDefinitionsGenerator>(source);

        var generated = string.Concat(result.RunResult.GeneratedTrees
            .Where(static t => t.FilePath.Contains("ProcessMetricDefinitions", StringComparison.Ordinal))
            .Select(static t => t.ToString()));

        // process.disk.operations is a Counter with unit {operation}, entity=process.
        generated.Should()
            .Contain("MetricDefinition<global::Qyl.Telemetry.SemanticConventions.Counter> ProcessDiskOperations")
            .And.Contain("name: \"process.disk.operations\"")
            .And.Contain("unit: \"{operation}\"")
            .And.Contain("entityAssociations: new string[] { \"process\" }");

        // system.process.limit is NOT under the "process" prefix (it's system.*) — must be absent.
        generated.Should().NotContain("system.process.limit");
    }
}
