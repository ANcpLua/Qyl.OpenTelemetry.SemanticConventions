// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   QYL0202 fires on a high-cardinality key on either metric path: a <c>[Tag]</c> parameter of
///   a <c>[Counter]</c>/<c>[Histogram]</c> method, or a literal key that reaches a metric
///   measurement through the SDK, including a <c>TagList</c> built in a local. The same key on
///   a span is not a metric tag and stays silent.
/// </summary>
public sealed class Qyl0202HighCardinalityMetricTagAnalyzerTests
{
    [Theory]
    [InlineData("counter.Add(1, new KeyValuePair<string, object?>(\"user.id\", 42));", "user.id")]
    [InlineData("histogram.Record(1.5, new KeyValuePair<string, object?>(\"url.full\", \"https://x\"));", "url.full")]
    [InlineData("_ = new Measurement(1, new KeyValuePair<string, object?>(\"customer.email\", \"a@b\"));", "customer.email")]
    [InlineData("var tags = new TagList(); tags.Add(\"request.id\", 1); counter.Add(1, tags);", "request.id")]
    [InlineData("var tags = new TagList { [\"sessionId\"] = 1 }; counter.Add(1, tags);", "sessionId")]
    public async Task Fires_on_a_high_cardinality_key_that_reaches_a_metric(string statement, string expectedKey)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0202HighCardinalityMetricTagAnalyzer(),
            Fixture(statement));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0202");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain(expectedKey);
    }

    [Theory]
    [InlineData("counter.Add(1, new KeyValuePair<string, object?>(\"http.route\", \"/orders/{id}\"));")]
    [InlineData("counter.Add(1, new KeyValuePair<string, object?>(\"enduser.id\", 42));")]
    [InlineData("activity.SetTag(\"user.id\", 42);")]
    [InlineData("var tags = new TagList(); tags.Add(\"request.id\", 1); source.StartActivity(\"operation\", tags: tags);")]
    public async Task Silent_on_a_bounded_key_or_on_a_key_that_reaches_a_span(string statement)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0202HighCardinalityMetricTagAnalyzer(),
            Fixture(statement));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Fires_on_a_tag_parameter_of_a_generated_counter()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0202HighCardinalityMetricTagAnalyzer(),
            Fixture(
                string.Empty,
                """
                [Counter("orders")]
                private static void CountOrder([Tag("orderId")] string id, [Tag("order.status")] string status) { }
                """));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("orderId");
    }

    private static string Fixture(string statement, string members = "") =>
        $$"""
        #nullable enable
        using System;
        using System.Collections.Generic;
        using Qyl.Instrumentation.Instrumentation;

        namespace Qyl.Instrumentation.Instrumentation
        {
            [AttributeUsage(AttributeTargets.Method)]
            public sealed class CounterAttribute(string name) : Attribute;

            [AttributeUsage(AttributeTargets.Method)]
            public sealed class HistogramAttribute(string name) : Attribute;

            [AttributeUsage(AttributeTargets.Parameter)]
            public sealed class TagAttribute(string name) : Attribute;
        }

        internal sealed class ActivitySource
        {
            public void StartActivity(
                string name,
                int kind = 0,
                object? parent = null,
                object? tags = null) { }
        }

        internal sealed class Counter
        {
            public void Add(int value, params KeyValuePair<string, object?>[] tags) { }
            public void Add(int value, TagList tags) { }
        }

        internal sealed class Histogram
        {
            public void Record(double value, params KeyValuePair<string, object?>[] tags) { }
        }

        internal sealed class Measurement(int value, params KeyValuePair<string, object?>[] tags);

        internal sealed class TagList
        {
            public object? this[string key] { set { } }
            public void Add(string key, object? value) { }
        }

        internal sealed class Activity
        {
            public Activity SetTag(string key, object? value) => this;
        }

        internal sealed class Sink
        {
            private readonly ActivitySource source = new();
            private readonly Counter counter = new();
            private readonly Histogram histogram = new();
            private readonly Activity activity = new();

            internal void Emit()
            {
                {{statement}}
            }

            {{members}}
        }
        """;
}
