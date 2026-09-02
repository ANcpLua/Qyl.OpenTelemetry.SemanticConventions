// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   QYL0008 fires on a direct incubating reference from a library, and stays silent for
///   the three accepted local-copy forms (const field, static readonly field, static
///   readonly array) plus a method-local const. A whole project opts out with
///   <c>OtelSemConvInstrumentationLibrary=true</c>.
/// </summary>
public sealed class Qyl0008IncubatingSemconvInLibraryAnalyzerTests
{
    /// <summary>
    ///   A stand-in incubating surface: the namespace carries both a
    ///   <c>SemanticConventions</c> root and an <c>.Incubating</c> segment, which is
    ///   exactly what <c>SemconvNamespace.IsIncubatingNamespace</c> recognises.
    /// </summary>
    private const string IncubatingFixture = """
        namespace Contoso.SemanticConventions.Incubating.Attributes.Messaging
        {
            public static class MessagingAttributes
            {
                public const string System = "messaging.system";
                public const string OperationType = "messaging.operation.type";
                public const string OperationName = "messaging.operation.name";
                public const string DestinationName = "messaging.destination.name";
            }
        }
        """;

    private static string Library(string body) =>
        $$"""
        using System.Diagnostics;
        using MessagingAttributes = Contoso.SemanticConventions.Incubating.Attributes.Messaging.MessagingAttributes;

        public static class Consumer
        {
        {{body}}
        }

        {{IncubatingFixture}}
        """;

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        IReadOnlyDictionary<string, string>? buildProperties = null)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new IncubatingSemconvInLibraryAnalyzer()],
            source,
            options: new AnalyzerOptions(
                [],
                new GlobalOptionsProvider(buildProperties ?? new Dictionary<string, string>())),
            excludeTestFrameworkReferences: true);
        return [.. diagnostics.Where(d => d.Id == "QYL0008")];
    }

    [Fact]
    public async Task Fires_on_a_direct_incubating_reference_in_a_library()
    {
        var diagnostics = await RunAsync(Library(
            """
                public static void M(Activity activity) =>
                    activity.SetTag(MessagingAttributes.System, "kafka");
            """));

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("System");
    }

    [Fact]
    public async Task Silent_for_a_const_field_copy()
    {
        var diagnostics = await RunAsync(Library(
            """
                private const string Copy = MessagingAttributes.System;

                public static void M(Activity activity) => activity.SetTag(Copy, "kafka");
            """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_for_a_static_readonly_field_copy()
    {
        var diagnostics = await RunAsync(Library(
            """
                private static readonly string Copy = MessagingAttributes.OperationType;

                public static void M(Activity activity) => activity.SetTag(Copy, "send");
            """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_for_a_static_readonly_array_of_copies()
    {
        var diagnostics = await RunAsync(Library(
            """
                private static readonly string[] Copies = [MessagingAttributes.OperationName];

                public static void M(Activity activity) => activity.SetTag(Copies[0], "publish");
            """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_for_a_method_local_const_copy()
    {
        var diagnostics = await RunAsync(Library(
            """
                public static void M(Activity activity)
                {
                    const string copy = MessagingAttributes.DestinationName;
                    activity.SetTag(copy, "orders");
                }
            """));

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_when_the_project_opts_out_as_an_instrumentation_library()
    {
        var diagnostics = await RunAsync(
            Library(
                """
                    public static void M(Activity activity) =>
                        activity.SetTag(MessagingAttributes.System, "kafka");
                """),
            new Dictionary<string, string>
            {
                ["build_property.OtelSemConvInstrumentationLibrary"] = "true",
            });

        diagnostics.Should().BeEmpty();
    }

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> global)
        : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options = new(global);
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                if (values.TryGetValue(key, out var v))
                {
                    value = v;
                    return true;
                }

                value = null!;
                return false;
            }
        }
    }
}
