// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   QYL0006 fires on a resource configuration that names no schema URL, on a receiver typed
///   as the builder itself (the shape every <c>WithTracing(builder => ...)</c> lambda has), and
///   stays silent when the schema URL arrives as a literal, as a constant named for it, or as a
///   constant whose value is the URL.
/// </summary>
public sealed class Qyl0006MissingSchemaUrlAnalyzerTests
{
    [Fact]
    public async Task Fires_when_the_resource_names_no_schema_url()
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0006MissingSchemaUrlAnalyzer(),
            Fixture("""builder.ConfigureResource(r => r.AddAttributes(new[] { new KeyValuePair<string, object>("service.name", "checkout") }));"""));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("QYL0006");
    }

    [Theory]
    [InlineData("""builder.ConfigureResource(r => r.AddAttributes(new[] { new KeyValuePair<string, object>("telemetry.schema_url", "https://opentelemetry.io/schemas/1.44.0") }));""")]
    [InlineData("""builder.ConfigureResource(r => r.AddAttributes(new[] { new KeyValuePair<string, object>(TelemetrySchemaUrl, SchemaUrl.Current) }));""")]
    [InlineData("""builder.ConfigureResource(r => r.AddAttributes(new[] { new KeyValuePair<string, object>(Key, Pins.Current) }));""")]
    [InlineData("""builder.ConfigureResource(r => r.AddAttributes(new[] { new KeyValuePair<string, object>(Key, "https://qyl.at/schemas/9.4.0") }));""")]
    public async Task Silent_when_the_schema_url_arrives_as_a_literal_a_named_constant_or_a_constant_value(string statement)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            new Qyl0006MissingSchemaUrlAnalyzer(),
            Fixture(statement));

        diagnostics.Should().BeEmpty();
    }

    private static string Fixture(string statement) =>
        $$"""
        #nullable enable
        using System;
        using System.Collections.Generic;
        using OpenTelemetry.Trace;

        namespace OpenTelemetry.Trace
        {
            public class ResourceBuilder
            {
                public ResourceBuilder AddAttributes(IEnumerable<KeyValuePair<string, object>> attributes) => this;
            }

            public abstract class TracerProviderBuilder
            {
                public TracerProviderBuilder ConfigureResource(Action<ResourceBuilder> configure) => this;
            }
        }

        public static class SchemaUrl
        {
            public const string Current = "https://opentelemetry.io/schemas/1.44.0";
        }

        public static class Pins
        {
            public const string Current = "https://qyl.at/schemas/9.4.0";
        }

        internal static class Startup
        {
            private const string TelemetrySchemaUrl = "telemetry.schema_url";
            private const string Key = "telemetry.schema_url";

            internal static void Configure(TracerProviderBuilder builder)
            {
                {{statement}}
            }
        }
        """;
}
