// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   QYL0601 fires on a sensitive-looking key only where the key provably reaches telemetry,
///   matches sensitive words on segment boundaries, and exempts keys the pinned registry
///   defines outright.
/// </summary>
public sealed class Qyl0601SensitiveDataInAttributeAnalyzerTests
{
    [Theory]
    [InlineData("activity.SetTag(\"user.password\", secret);", "user.password")]
    [InlineData("activity.SetBaggage(\"api_key\", secret);", "api_key")]
    [InlineData("activity.SetTag(\"x-auth-token\", secret);", "x-auth-token")]
    [InlineData("activity.SetTag(\"apiKey\", secret);", "apiKey")]
    [InlineData("counter.Add(1, new KeyValuePair<string, object?>(\"credit_card\", secret));", "credit_card")]
    [InlineData("var tags = new TagList(); tags.Add(\"connection_string\", secret);", "connection_string")]
    public async Task Fires_where_a_sensitive_key_reaches_telemetry(string statement, string expectedKey)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture(statement));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0601");
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain(expectedKey);
    }

    [Fact]
    public async Task A_dictionary_reaches_telemetry_through_the_local_it_is_built_in()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture(
                """
                var attributes = new Dictionary<string, object?>();
                attributes["password"] = secret;
                source.StartActivity("operation", tags: attributes);
                """));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("password");
    }

    [Fact]
    public async Task A_dictionary_that_never_reaches_telemetry_is_silent_whatever_it_is_called()
    {
        // The previous detection fired here because the receiver was named "attributes".
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture(
                """
                var attributes = new Dictionary<string, object?>();
                attributes["password"] = secret;
                """));

        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("activity.SetTag(\"gen_ai.usage.input_tokens\", 42);")]
    [InlineData("activity.SetTag(\"app.pipeline.stage\", \"build\");")]
    [InlineData("activity.SetTag(\"app.author.name\", \"x\");")]
    public async Task Sensitive_words_match_whole_segments_not_substrings(string statement)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture(statement));

        diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("activity.SetTag(\"aspnetcore.authorization.policy\", \"admin\");")]
    [InlineData("activity.SetTag(\"aws.secretsmanager.secret.arn\", \"arn:aws:secretsmanager:eu-west-1:123456789012:secret:x\");")]
    public async Task A_key_the_registry_defines_outright_is_exempt(string statement)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture(statement));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task A_key_that_only_extends_a_registry_template_is_not_exempt()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0601SensitiveDataInAttributeAnalyzer(),
            Fixture("activity.SetTag(\"http.request.header.authorization\", secret);"));

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("http.request.header.authorization");
    }

    private static string Fixture(string statement) =>
        $$"""
        #nullable enable
        using System;
        using System.Collections.Generic;

        internal sealed class ActivitySource
        {
            public void StartActivity(
                string name,
                int kind = 0,
                object? parent = null,
                IEnumerable<KeyValuePair<string, object?>>? tags = null) { }
        }

        internal sealed class Counter
        {
            public void Add(int value, params KeyValuePair<string, object?>[] tags) { }
        }

        internal sealed class TagList
        {
            public object? this[string key] { set { } }
            public void Add(string key, object? value) { }
        }

        internal sealed class Activity
        {
            public Activity SetTag(string key, object? value) => this;
            public Activity SetBaggage(string key, string? value) => this;
        }

        internal sealed class Sink
        {
            private readonly ActivitySource source = new();
            private readonly Counter counter = new();
            private readonly Activity activity = new();

            internal void Emit(string secret)
            {
                {{statement}}
            }
        }
        """;
}
