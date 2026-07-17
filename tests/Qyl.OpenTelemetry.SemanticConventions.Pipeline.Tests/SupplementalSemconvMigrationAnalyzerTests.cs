using System.Collections.Immutable;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

public sealed class SupplementalSemconvMigrationAnalyzerTests
{
    public static TheoryData<string, string, int> PayloadContexts => new()
    {
        {
            "SetTag(\"gen_ai.system\", \"openai\");",
            "QYL0009",
            1
        },
        {
            "AddTag(\"gen_ai.system\", \"openai\");",
            "QYL0009",
            1
        },
        {
            "var values = new Dictionary<string, object?>(); values.Add(\"gen_ai.system\", \"openai\");",
            "QYL0010",
            1
        },
        {
            "var tags = new Dictionary<string, object?>(); tags.Add(\"gen_ai.system\", \"openai\"); source.StartActivity(\"operation\", tags: tags);",
            "QYL0009",
            1
        },
        {
            "var tags = new Dictionary<string, object?> { [\"gen_ai.system\"] = \"openai\" }; source.StartActivity(\"operation\", tags: tags);",
            "QYL0009",
            1
        },
        {
            "counter.Add(1, new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\"));",
            "QYL0009",
            1
        },
        {
            "_ = new Measurement(1, new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\"));",
            "QYL0009",
            1
        },
        {
            "logger.BeginScope(new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") });",
            "QYL0009",
            1
        },
        {
            "logger.Log(0, 0, new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") });",
            "QYL0009",
            1
        },
        {
            "_ = new ActivityEvent(\"event\", null, new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") });",
            "QYL0009",
            1
        },
        {
            "_ = new ActivityLink(new object(), new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") });",
            "QYL0009",
            1
        },
        {
            "KeyValuePair<string, object?>[] tags = [.. new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") }]; source.StartActivity(\"operation\", tags: tags);",
            "QYL0009",
            1
        },
        {
            "tagList[\"gen_ai.system\"] = \"openai\";",
            "QYL0009",
            1
        },
        {
            "resourceBuilder.AddAttributes(new[] { new KeyValuePair<string, object?>(\"gen_ai.system\", \"openai\") });",
            "QYL0009",
            1
        },
    };

    [Theory]
    [MemberData(nameof(PayloadContexts))]
    public async Task Shared_payload_detection_preserves_supplemental_contexts(
        string statement,
        string expectedId,
        int expectedCount)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture(statement));

        diagnostics.Should().HaveCount(expectedCount);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == expectedId);
    }

    [Theory]
    [InlineData("meter.CreateCounter(\"system.memory.shared\");", "QYL0009")]
    [InlineData("activity.AddEvent(\"rpc.message\");", "QYL0010")]
    [InlineData("_ = new ActivityEvent(\"rpc.message\", null, null);", "QYL0010")]
    public async Task Metric_and_event_name_paths_remain_owned(string statement, string expectedId)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture(statement));

        diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == expectedId);
    }

    [Fact]
    public async Task Constant_key_is_not_reclassified_as_a_hard_coded_literal()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture("SetTag(GenAiSystem, \"openai\");"));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Bare_value_is_reported_but_constant_value_is_not()
    {
        var bare = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture("SetTag(\"cloud.platform\", \"azure_aks\");"));
        var constant = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture("SetTag(\"cloud.platform\", AzureAks);"));

        bare.Should().ContainSingle(diagnostic => diagnostic.Id == "QYL0009");
        constant.Should().BeEmpty();
    }

    [Fact]
    public async Task Catalog_owner_source_is_not_diagnosed()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture("var values = new Dictionary<string, object?>(); values.Add(\"gen_ai.system\", \"openai\");"),
            "/repo/OpenTelemetryDeprecatedSemconvCatalog.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Generated_source_is_excluded_by_Roslyn()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer()],
            Fixture("SetTag(\"gen_ai.system\", \"openai\");"),
            "/repo/Telemetry.g.cs");

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Catalog_fallback_reports_one_terminal_GenAi_replacement_without_live_metadata()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer(), new LiteralMatchesDeprecatedSemconvAnalyzer()],
            Fixture("SetTag(\"gen_ai.system\", \"openai\");"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0009");
        diagnostics[0].Properties["ReplacementName"].Should().Be("gen_ai.provider.name");
    }

    [Fact]
    public async Task Live_metadata_wins_with_one_terminal_GenAi_replacement()
    {
        var liveSemconvReference = AnalyzerHarness.CompileReference(
            """
            namespace OpenTelemetry.SemanticConventions.Attributes.GenAi
            {
                public static class GenAiAttributes
                {
                    [System.Obsolete("Replaced by gen_ai.provider.name.")]
                    public const string AttributeGenAiSystem = "gen_ai.system";
                }
            }
            """);

        var diagnostics = await AnalyzerHarness.RunAsync(
            [new SupplementalSemconvMigrationAnalyzer(), new LiteralMatchesDeprecatedSemconvAnalyzer()],
            Fixture("SetTag(\"gen_ai.system\", \"openai\");"),
            additionalReferences: [liveSemconvReference]);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0005");
        diagnostics[0].Properties["ReplacementValue"].Should().Be("gen_ai.provider.name");
    }

    private static string Fixture(string statement) =>
        $$"""
        #nullable enable
        using System;
        using System.Collections.Generic;

        internal interface ILogger
        {
            void BeginScope(object state);
            void Log(int level, int eventId, object state);
        }

        internal sealed class ActivitySource
        {
            public void StartActivity(
                string name,
                int kind = 0,
                object? parent = null,
                IEnumerable<KeyValuePair<string, object?>>? tags = null) { }
        }

        internal sealed class ActivityEvent(string name, object? timestamp, object? tags);
        internal sealed class ActivityLink(object context, object? tags);

        internal sealed class Counter
        {
            public void Add(int value, params KeyValuePair<string, object?>[] tags) { }
        }

        internal sealed class Meter
        {
            public Counter CreateCounter(string name) => new();
        }

        internal sealed class Activity
        {
            public void AddEvent(string name) { }
        }

        internal sealed class Measurement(
            int value,
            params KeyValuePair<string, object?>[] tags);

        internal sealed class TagList
        {
            public object? this[string key] { set { } }
            public void Add(string key, object? value) { }
        }

        internal sealed class ResourceBuilder
        {
            public void AddAttributes(IEnumerable<KeyValuePair<string, object?>> attributes) { }
        }

        internal sealed class Sink
        {
            private const string GenAiSystem = "gen_ai.system";
            private const string AzureAks = "azure_aks";
            private readonly ActivitySource source = new();
            private readonly Counter counter = new();
            private readonly Meter meter = new();
            private readonly Activity activity = new();
            private readonly ILogger logger = null!;
            private readonly TagList tagList = new();
            private readonly ResourceBuilder resourceBuilder = new();

            private void SetTag(string key, object? value) { }
            private void AddTag(string key, object? value) { }

            internal void Emit()
            {
                {{statement}}
            }
        }
        """;
}
