using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Build-time shape gate for the embedded resolved registry. The extractors parse
/// tolerantly at analyzer-load time — an entry whose shape regresses is skipped
/// with no diagnostic, so a generate.sh/template change could silently drop
/// attributes from the generated surface. This gate asserts, at CI time, that no
/// entry in the shipped registry would be skipped by the loaders' shape checks.
/// Loud here, tolerant in the analyzer host: a single odd entry must fail our
/// build, not crash the generator on every consumer's machine.
/// </summary>
public sealed class RegistryShapeGateTests
{
    private static JsonElement LoadRoot()
    {
        using var stream = typeof(SemConvAttributesGenerator).Assembly.GetManifestResourceStream(
            "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json");
        stream.Should().NotBeNull("the resolved registry must be embedded in the generator assembly");
        using var doc = JsonDocument.Parse(stream!);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void Root_has_catalog_and_groups_arrays()
    {
        var root = LoadRoot();
        root.ValueKind.Should().Be(JsonValueKind.Object);
        root.GetProperty("catalog").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("groups").ValueKind.Should().Be(JsonValueKind.Array);
        root.GetProperty("catalog").GetArrayLength().Should().BeGreaterThan(500,
            "a drastically shrunken catalog indicates a resolution regression");
    }

    [Fact]
    public void Every_catalog_entry_has_the_shape_the_loaders_require()
    {
        foreach (var attr in LoadRoot().GetProperty("catalog").EnumerateArray())
        {
            attr.ValueKind.Should().Be(JsonValueKind.Object,
                "the loaders skip non-object catalog entries silently");
            attr.GetProperty("key").GetString().Should().NotBeNullOrWhiteSpace();

            attr.TryGetProperty("type", out var type).Should().BeTrue(
                $"catalog entry '{attr.GetProperty("key").GetString()}' must declare a type");
            switch (type.ValueKind)
            {
                case JsonValueKind.String:
                    type.GetString().Should().NotBeNullOrWhiteSpace();
                    break;
                case JsonValueKind.Object:
                    type.GetProperty("members").ValueKind.Should().Be(JsonValueKind.Array,
                        $"enum-typed entry '{attr.GetProperty("key").GetString()}' must carry members");
                    foreach (var member in type.GetProperty("members").EnumerateArray())
                    {
                        member.ValueKind.Should().Be(JsonValueKind.Object,
                            "the loaders skip non-object enum members silently");
                        member.GetProperty("id").GetString().Should().NotBeNullOrWhiteSpace();
                    }
                    break;
                default:
                    Assert.Fail($"catalog entry '{attr.GetProperty("key").GetString()}' has unsupported type kind {type.ValueKind}");
                    break;
            }
        }
    }

    [Fact]
    public void Every_metric_entry_has_the_shape_the_loaders_require()
    {
        var metrics = LoadRoot().GetProperty("metrics");
        metrics.ValueKind.Should().Be(JsonValueKind.Array);
        metrics.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var metric in metrics.EnumerateArray())
        {
            metric.ValueKind.Should().Be(JsonValueKind.Object,
                "the loaders skip non-object metric entries silently");
            metric.GetProperty("metric_name").GetString().Should().NotBeNullOrWhiteSpace();
            metric.GetProperty("instrument").GetString().Should().NotBeNullOrWhiteSpace(
                $"metric '{metric.GetProperty("metric_name").GetString()}' must declare an instrument");
            metric.GetProperty("unit").GetString().Should().NotBeNull(
                $"metric '{metric.GetProperty("metric_name").GetString()}' must declare a unit");
        }
    }

    [Fact]
    public void Every_event_entry_has_the_shape_the_loaders_require()
    {
        var events = LoadRoot().GetProperty("events");
        events.ValueKind.Should().Be(JsonValueKind.Array);
        events.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var ev in events.EnumerateArray())
        {
            ev.ValueKind.Should().Be(JsonValueKind.Object,
                "the loaders skip non-object event entries silently");
            ev.GetProperty("event_name").GetString().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Every_group_entry_has_the_shape_the_loaders_require()
    {
        foreach (var group in LoadRoot().GetProperty("groups").EnumerateArray())
        {
            group.ValueKind.Should().Be(JsonValueKind.Object,
                "the loaders skip non-object groups silently");
            var id = group.GetProperty("id").GetString();
            id.Should().NotBeNullOrWhiteSpace();

            var hasAttributes = group.TryGetProperty("attributes", out var attributes);
            var hasRefs = group.TryGetProperty("attribute_refs", out var refs);

            if (hasAttributes)
            {
                attributes.ValueKind.Should().Be(JsonValueKind.Array);
                foreach (var attr in attributes.EnumerateArray())
                {
                    attr.ValueKind.Should().Be(JsonValueKind.Object,
                        $"group '{id}' contains a non-object attribute entry the loaders would skip");
                    attr.GetProperty("key").GetString().Should().NotBeNullOrWhiteSpace();
                }
            }

            if (hasRefs)
            {
                refs.ValueKind.Should().Be(JsonValueKind.Array);
                foreach (var attrRef in refs.EnumerateArray())
                    attrRef.ValueKind.Should().Be(JsonValueKind.String,
                        $"group '{id}' contains a non-string attribute_ref the loaders would skip");
            }
        }
    }
}
