using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

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
            "Qyl.Telemetry.SemanticConventions.SourceGeneration.resolved-registry.json");
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
    public void Root_records_all_three_registry_sources_and_the_upstream_manifests()
    {
        var root = LoadRoot();
        var sources = root.GetProperty("sources").EnumerateArray().ToArray();
        sources.Should().HaveCount(3, "core, genai, and the qyl-owned registry");

        sources.Single(source => source.GetProperty("source_registry").GetString() == "core")
            .GetProperty("source_ref").GetString().Should().Be("v1.44.0");
        sources.Single(source => source.GetProperty("source_registry").GetString() == "genai")
            .GetProperty("source_commit").GetString().Should()
            .Be("eaefa142a94cefe5d199d47e4a73727dfbd825df");

        // qyl: ref is the file, commit is the SHA-256 of its bytes, and there is no schema
        // URL or upstream date to cite.
        var qyl = sources.Single(source => source.GetProperty("source_registry").GetString() == "qyl");
        qyl.GetProperty("source_ref").GetString().Should().Be("qyl-registry.json");
        qyl.GetProperty("source_commit").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        qyl.GetProperty("schema_url").ValueKind.Should().Be(JsonValueKind.Null);
        qyl.GetProperty("source_date_epoch").ValueKind.Should().Be(JsonValueKind.Null);

        root.GetProperty("manifests").EnumerateArray()
            .Select(manifest => manifest.GetProperty("source_registry").GetString())
            .Should().BeEquivalentTo(["core", "genai"], "only the upstream registries publish a manifest");
    }

    [Fact]
    public void Complete_pinned_model_file_inventory_is_fingerprinted()
    {
        var files = LoadRoot().GetProperty("model_files").EnumerateArray().ToArray();
        files.Count(file => file.GetProperty("source_registry").GetString() == "core").Should().Be(243);
        files.Count(file => file.GetProperty("source_registry").GetString() == "genai").Should().Be(19);

        foreach (var file in files)
        {
            file.GetProperty("path").GetString().Should().StartWith("model/");
            file.GetProperty("kind").GetString().Should().NotBeNullOrWhiteSpace();
            file.GetProperty("sha256").GetString().Should().MatchRegex("^[0-9a-f]{64}$");
        }
    }

    [Fact]
    public void Every_genai_any_attribute_has_one_authoritative_json_schema()
    {
        var root = LoadRoot();
        var anyAttributes = root.GetProperty("catalog").EnumerateArray()
            .Where(attribute => attribute.GetProperty("source_registry").GetString() == "genai")
            .Where(attribute => attribute.GetProperty("type").ValueKind == JsonValueKind.String)
            .Where(attribute => attribute.GetProperty("type").GetString() == "any")
            .Select(attribute => attribute.GetProperty("key").GetString())
            .ToArray();

        var schemas = root.GetProperty("json_schemas").EnumerateArray().ToArray();
        schemas.Should().HaveCount(8);
        schemas.SelectMany(schema => schema.GetProperty("attribute_keys").EnumerateArray())
            .Select(key => key.GetString())
            .Should().BeEquivalentTo(anyAttributes);

        foreach (var schema in schemas)
        {
            schema.GetProperty("source_registry").GetString().Should().Be("genai");
            var content = schema.GetProperty("content").GetString();
            content.Should().NotBeNullOrWhiteSpace();
            using var document = JsonDocument.Parse(content!);
            document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);

            var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content!)))
                .ToLowerInvariant();
            actualHash.Should().Be(schema.GetProperty("sha256").GetString());
        }
    }

    [Fact]
    public void Moved_genai_protocol_and_provider_namespaces_come_only_from_genai_source()
    {
        string[] movedPrefixes = ["gen_ai.", "mcp.", "openai.", "aws.bedrock."];
        var movedAttributes = LoadRoot().GetProperty("catalog").EnumerateArray()
            .Where(attribute => movedPrefixes.Any(prefix =>
                attribute.GetProperty("key").GetString()!.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        movedAttributes.Should().NotBeEmpty();
        movedAttributes.Should().OnlyContain(attribute =>
            attribute.GetProperty("source_registry").GetString() == "genai");
    }

    [Fact]
    public void Qyl_registry_is_merged_as_the_third_source()
    {
        // generate.sh merges Resources/qyl-registry.json into the projection: attributes join
        // the catalog tagged source_registry "qyl", metrics join metrics and groups with their
        // attribute references resolved against the merged catalog, and the qyl-owned scope
        // and event names land at the root. Every downstream projection reads this one file.
        var root = LoadRoot();

        var qylAttributes = root.GetProperty("catalog").EnumerateArray()
            .Where(attribute => attribute.GetProperty("key").GetString()!.StartsWith("qyl.", StringComparison.Ordinal))
            .ToArray();
        qylAttributes.Should().NotBeEmpty();
        qylAttributes.Should().OnlyContain(attribute => attribute.GetProperty("source_registry").GetString() == "qyl");

        // Every qyl row carries the third source's provenance: the same ref and commit as the
        // qyl `sources` entry, and null schema_url / source_date_epoch.
        var qylCommit = root.GetProperty("sources").EnumerateArray()
            .Single(source => source.GetProperty("source_registry").GetString() == "qyl")
            .GetProperty("source_commit").GetString();
        var qylRows = root.GetProperty("catalog").EnumerateArray()
            .Concat(root.GetProperty("metrics").EnumerateArray())
            .Concat(root.GetProperty("groups").EnumerateArray())
            .Where(row => row.GetProperty("source_registry").GetString() == "qyl")
            .ToArray();
        qylRows.Should().HaveCount(28 + 1 + 1, "28 attributes, one metric, and its group");
        foreach (var row in qylRows)
        {
            row.GetProperty("source_ref").GetString().Should().Be("qyl-registry.json");
            row.GetProperty("source_commit").GetString().Should().Be(qylCommit);
            row.GetProperty("schema_url").ValueKind.Should().Be(JsonValueKind.Null);
            row.GetProperty("source_date_epoch").ValueKind.Should().Be(JsonValueKind.Null);
        }

        // The value is the wire token; the id is the C# identifier source (every emitter names
        // members from PascalCase(id)), so both are published API and both are pinned here.
        // The ids are snake_case rather than the dotted value because ToPascalCase splits on
        // '.' identically to '_': "db.mongodb" would emit DbMongodb, "db_mongo_db" emits
        // DbMongoDb. qyl's log-as-span lane is deleted, so no log.* domain remains.
        var domain = qylAttributes.Single(attribute => attribute.GetProperty("key").GetString() == "qyl.instrumentation.domain");
        var members = domain.GetProperty("type").GetProperty("members").EnumerateArray()
            .Select(member => (Id: member.GetProperty("id").GetString(), Value: member.GetProperty("value").GetString()))
            .ToArray();
        members.Should().HaveCount(18);
        members.Should().BeEquivalentTo(
        [
            (Id: "asp_net_core_server", Value: "aspnetcore.server"),
            (Id: "azure_sdk", Value: "azure.sdk"),
            (Id: "db_client", Value: "db.client"),
            (Id: "db_ef_core", Value: "db.efcore"),
            (Id: "db_elasticsearch", Value: "db.elasticsearch"),
            (Id: "db_mongo_db", Value: "db.mongodb"),
            (Id: "db_redis", Value: "db.redis"),
            (Id: "db_sql_client", Value: "db.sqlclient"),
            (Id: "elastic_transport", Value: "elastic.transport"),
            (Id: "graph_ql", Value: "graphql"),
            (Id: "http_client", Value: "http.client"),
            (Id: "job_quartz", Value: "job.quartz"),
            (Id: "messaging_kafka", Value: "messaging.kafka"),
            (Id: "messaging_mass_transit", Value: "messaging.masstransit"),
            (Id: "messaging_n_service_bus", Value: "messaging.nservicebus"),
            (Id: "messaging_rabbit_mq", Value: "messaging.rabbitmq"),
            (Id: "rpc_grpc", Value: "rpc.grpc"),
            (Id: "rpc_wcf_client", Value: "rpc.wcf.client"),
        ]);
        members.Should().OnlyContain(member => !member.Value!.StartsWith("log.", StringComparison.Ordinal));

        var metric = root.GetProperty("metrics").EnumerateArray()
            .Single(metric => metric.GetProperty("metric_name").GetString() == "nservicebus.messaging.operation.duration");
        metric.GetProperty("source_registry").GetString().Should().Be("qyl");
        metric.GetProperty("instrument").GetString().Should().Be("histogram");
        metric.GetProperty("unit").GetString().Should().Be("s");
        metric.GetProperty("stability").GetString().Should().Be("development");
        metric.GetProperty("attributes").EnumerateArray()
            .Select(attribute => attribute.GetProperty("key").GetString())
            .Should().BeEquivalentTo(["messaging.system", "messaging.operation.type", "messaging.operation.name"]);
        metric.GetProperty("attributes").EnumerateArray()
            .Should().OnlyContain(attribute => attribute.GetProperty("type").ValueKind != JsonValueKind.Undefined,
                "metric attribute references are resolved against the merged catalog");
        root.GetProperty("groups").EnumerateArray()
            .Should().Contain(group => group.GetProperty("id").GetString() == "metric.nservicebus.messaging.operation.duration");

        root.GetProperty("scope_names").EnumerateArray().Select(name => name.GetString())
            .Should().Contain("Qyl.Telemetry.AutoInstrumentation").And.BeInAscendingOrder();
        root.GetProperty("event_names").EnumerateArray().Select(name => name.GetString())
            .Should().Contain("qyl.agent.diagnostic.snapshot").And.BeInAscendingOrder();
    }

    [Fact]
    public void Qyl_rows_stay_inside_their_namespace_and_shadow_no_upstream_row()
    {
        // The inverse of merge_registries.py's guard, asserted on the shipped projection:
        // the merge is last-wins, so a qyl row keyed like an upstream row would have replaced
        // it silently. Every qyl-sourced attribute is qyl.*, every qyl.* attribute is
        // qyl-sourced, and no key, metric name, or group id appears twice.
        var root = LoadRoot();

        var catalog = root.GetProperty("catalog").EnumerateArray()
            .Select(attribute => (Key: attribute.GetProperty("key").GetString()!, Source: attribute.GetProperty("source_registry").GetString()))
            .ToArray();
        catalog.Select(row => row.Key).Should().OnlyHaveUniqueItems();
        catalog.Where(row => row.Source == "qyl").Should().OnlyContain(row => row.Key.StartsWith("qyl.", StringComparison.Ordinal));
        catalog.Where(row => row.Key.StartsWith("qyl.", StringComparison.Ordinal)).Should().OnlyContain(row => row.Source == "qyl");

        var metrics = root.GetProperty("metrics").EnumerateArray()
            .Select(metric => (Name: metric.GetProperty("metric_name").GetString()!, Source: metric.GetProperty("source_registry").GetString()))
            .ToArray();
        metrics.Select(row => row.Name).Should().OnlyHaveUniqueItems();
        var upstreamMetricNames = metrics.Where(row => row.Source != "qyl").Select(row => row.Name).ToHashSet(StringComparer.Ordinal);
        metrics.Where(row => row.Source == "qyl").Should().OnlyContain(row => !upstreamMetricNames.Contains(row.Name));

        var groups = root.GetProperty("groups").EnumerateArray()
            .Select(group => (Id: group.GetProperty("id").GetString()!, Type: group.GetProperty("type").GetString()!, Source: group.GetProperty("source_registry").GetString()))
            .ToArray();
        groups.Select(row => (row.Type, row.Id)).Should().OnlyHaveUniqueItems();
        var upstreamGroupIds = groups.Where(row => row.Source != "qyl").Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        groups.Where(row => row.Source == "qyl").Should().OnlyContain(row => !upstreamGroupIds.Contains(row.Id));
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
    public void Public_registry_stream_retains_named_event_entries()
    {
        var events = LoadRoot().GetProperty("events");
        events.ValueKind.Should().Be(JsonValueKind.Array);
        events.GetArrayLength().Should().BeGreaterThan(0);
        foreach (var ev in events.EnumerateArray())
        {
            ev.ValueKind.Should().Be(JsonValueKind.Object,
                "the public resolved-registry stream preserves structured event rows");
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
