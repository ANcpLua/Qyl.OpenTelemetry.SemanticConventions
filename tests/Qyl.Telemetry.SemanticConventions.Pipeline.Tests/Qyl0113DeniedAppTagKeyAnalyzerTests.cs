using System.Globalization;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>QYL0113: a business key with a token the collector denies is dropped at ingest, so it is an error at build.</summary>
public sealed class Qyl0113DeniedAppTagKeyAnalyzerTests
{
    [Fact]
    public async Task Reports_an_app_key_that_contains_a_denied_token()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0113DeniedAppTagKeyAnalyzer(),
            "QYL0113",
            """
            using System.Diagnostics;
            using Qyl;

            public static class Store
            {
                public static void Write(Activity span)
                {
                    span.SetAppTag("order.message", "x");
                    span.SetAppTag("customer.Password", "x");
                    span.SetTag("qyl.app.session_token", "x");
                }
            }
            """);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Should()
            .Contain(static m => m.Contains("qyl.app.order.message", StringComparison.Ordinal) && m.Contains("'message'", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("'password'", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("'token'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reports_an_app_key_whose_name_the_registry_does_not_persist()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0113DeniedAppTagKeyAnalyzer(),
            "QYL0113",
            """
            using System.Diagnostics;
            using Qyl;

            public static class Store
            {
                public static void Write(Activity span)
                {
                    span.SetAppTag("user.email", "x");
                    span.SetAppTag("url.full", "x");
                    span.SetTag("qyl.app.enduser.id", "x");
                }
            }
            """);

        diagnostics.Should().HaveCount(3);
        diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Should()
            .Contain(static m => m.Contains("qyl.app.user.email", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("'url.full'", StringComparison.Ordinal))
            .And.Contain(static m => m.Contains("qyl.app.enduser.id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Is_silent_for_a_persisted_app_key_and_for_registry_keys()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0113DeniedAppTagKeyAnalyzer(),
            "QYL0113",
            """
            using System.Diagnostics;
            using Qyl;

            public static class Store
            {
                public static void Write(Activity span)
                {
                    span.SetAppTag("order.id", "ord-1042");
                    span.SetTag("messaging.message.id", "m-1");
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
