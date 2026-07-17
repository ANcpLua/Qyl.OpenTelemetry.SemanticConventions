using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   Tests for the narrowed QYL0304 trigger: it fires only when an AOT publish (PublishAot=true,
///   EventSourceSupport unset) actually contains in-process EventSource/EventListener instrumentation —
///   not on every AOT project.
/// </summary>
public sealed class Qyl0304EnableEventSourceSupportAnalyzerTests
{
    private const string EventSourceSubclass = """
        using System.Diagnostics.Tracing;
        public sealed class MySource : EventSource { }
        """;

    private const string EventListenerSubclass = """
        using System.Diagnostics.Tracing;
        public sealed class MyListener : EventListener { }
        """;

    private const string PlainClass = "public class C { }";

    private static readonly Dictionary<string, string> AotNoSwitch =
        new() { ["build_property.PublishAot"] = "true" };

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        IReadOnlyDictionary<string, string> buildProperties)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new Qyl0304EnableEventSourceSupportAnalyzer()],
            source,
            options: new AnalyzerOptions([], new GlobalOptionsProvider(buildProperties)));
        return [.. diagnostics.Where(d => d.Id == "QYL0304")];
    }

    [Fact]
    public async Task Fires_when_aot_and_eventsource_subclass_present()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("MySource");
    }

    [Fact]
    public async Task Fires_when_aot_and_eventlistener_subclass_present()
    {
        var diagnostics = await RunAsync(EventListenerSubclass, AotNoSwitch);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("MyListener");
    }

    [Fact]
    public async Task Silent_on_plain_aot_app_without_eventsource()
    {
        // The whole point of the narrowing: a pure OTLP-push AOT app is not nagged.
        var diagnostics = await RunAsync(PlainClass, AotNoSwitch);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_when_eventsourcesupport_enabled()
    {
        var diagnostics = await RunAsync(
            EventSourceSubclass,
            new Dictionary<string, string>
            {
                ["build_property.PublishAot"] = "true",
                ["build_property.EventSourceSupport"] = "true"
            });

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_when_not_publishing_aot()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, new Dictionary<string, string>());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Report_location_points_at_the_type()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);

        diagnostics[0].Location.IsInSource.Should().BeTrue();
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
