using System.Text.Json;
using AwesomeAssertions;
using Qyl.OpenTelemetry.SemanticConventions.Incubating.Registry;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

public sealed class SemanticConventionRegistryTests
{
    private static readonly string[] s_payloadAttributes =
    [
        "gen_ai.input.messages",
        "gen_ai.memory.records",
        "gen_ai.output.messages",
        "gen_ai.retrieval.documents",
        "gen_ai.system_instructions",
        "gen_ai.tool.call.arguments",
        "gen_ai.tool.call.result",
        "gen_ai.tool.definitions",
    ];

    [Fact]
    public void Public_registry_reports_exact_source_identity()
    {
        SemanticConventionRegistry.CoreSchemaUrl.Should()
            .Be("https://opentelemetry.io/schemas/1.43.0");
        SemanticConventionRegistry.GenAiSourceCommit.Should()
            .Be("33b7f9da9ade6162d4a5c16247d0bc6ad5f8b469");
        SemanticConventionRegistry.WeaverVersion.Should().Be("0.24.2");
    }

    [Fact]
    public void Complete_resolved_registry_is_publicly_readable()
    {
        using var stream = SemanticConventionRegistry.OpenResolvedRegistry();
        using var document = JsonDocument.Parse(stream);

        document.RootElement.GetProperty("sources").GetArrayLength().Should().Be(2);
        document.RootElement.GetProperty("model_files").EnumerateArray()
            .Count(file => file.GetProperty("source_registry").GetString() == "genai")
            .Should().Be(19);
        document.RootElement.GetProperty("json_schemas").GetArrayLength().Should().Be(8);
        document.RootElement.GetProperty("events").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Every_structured_genai_attribute_opens_its_upstream_schema()
    {
        SemanticConventionRegistry.PayloadAttributeKeys.Should().BeEquivalentTo(s_payloadAttributes);

        foreach (var attribute in s_payloadAttributes)
        {
            SemanticConventionRegistry.TryOpenPayloadSchema(attribute, out var schema).Should().BeTrue();
            schema.Should().NotBeNull();
            using var openedSchema = schema!;
            using var document = JsonDocument.Parse(openedSchema);
            document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        }
    }

    [Fact]
    public void Unknown_payload_attribute_has_no_schema()
    {
        SemanticConventionRegistry.TryOpenPayloadSchema("gen_ai.unknown", out var schema)
            .Should().BeFalse();
        schema.Should().BeNull();
    }
}
