using System.Globalization;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>QYL0113: a business key with a token the collector denies is dropped at ingest, so it is an error at build.</summary>
[TestClass]
public sealed class Qyl0113DeniedAppTagKeyAnalyzerTests(TestContext testContext)
{

    [TestMethod]
    public async Task Reports_an_app_key_that_contains_a_denied_token()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0113DeniedAppTagKeyAnalyzer(), "QYL0113", """
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
            """, cancellationToken: testContext.CancellationToken);
        Assert.HasCount(3, diagnostics);
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("qyl.app.order.message", StringComparison.Ordinal) && m.Contains("'message'", StringComparison.Ordinal)));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("'password'", StringComparison.Ordinal)));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("'token'", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Reports_an_app_key_whose_name_the_registry_does_not_persist()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0113DeniedAppTagKeyAnalyzer(), "QYL0113", """
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
            """, cancellationToken: testContext.CancellationToken);
        Assert.HasCount(3, diagnostics);
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("qyl.app.user.email", StringComparison.Ordinal)));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("'url.full'", StringComparison.Ordinal)));
        Assert.IsTrue(diagnostics.Select(static d => d.GetMessage(CultureInfo.InvariantCulture)).Any(static m => m.Contains("qyl.app.enduser.id", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Is_silent_for_a_persisted_app_key_and_for_registry_keys()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0113DeniedAppTagKeyAnalyzer(), "QYL0113", """
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
            """, cancellationToken: testContext.CancellationToken);
        Assert.IsEmpty(diagnostics);
    }
}
