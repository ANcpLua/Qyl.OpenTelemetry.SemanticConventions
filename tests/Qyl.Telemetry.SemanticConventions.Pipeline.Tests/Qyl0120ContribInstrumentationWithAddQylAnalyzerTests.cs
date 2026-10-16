using System.Globalization;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>QYL0120: AddQyl already records what the OpenTelemetry.Instrumentation.* packages would, twice (ADR D6).</summary>
[TestClass]
public sealed class Qyl0120ContribInstrumentationWithAddQylAnalyzerTests(TestContext testContext)
{

    private static readonly (string, string) OpenTelemetry = ("OpenTelemetry", """
        namespace OpenTelemetry.Trace
        {
            public abstract class TracerProviderBuilder
            {
                public abstract TracerProviderBuilder AddSource(params string[] names);
            }
        }
        """);
    private static readonly (string, string) AspNetCoreInstrumentation = ("OpenTelemetry.Instrumentation.AspNetCore", """
        namespace OpenTelemetry.Trace
        {
            public static class AspNetCoreInstrumentationTracerProviderBuilderExtensions
            {
                public static TracerProviderBuilder AddAspNetCoreInstrumentation(this TracerProviderBuilder builder) => builder;
            }
        }
        """);
    private static readonly (string, string) ContosoExtensions = ("Contoso.Telemetry", """
        namespace Contoso
        {
            public static class ContosoTracerProviderBuilderExtensions
            {
                public static OpenTelemetry.Trace.TracerProviderBuilder AddContoso(this OpenTelemetry.Trace.TracerProviderBuilder builder) => builder;
            }
        }
        """);
    private static readonly (string, string) Hosting = ("Qyl.Telemetry.Hosting", """
        namespace Qyl
        {
            public sealed class QylTelemetryOptions
            {
                public System.Action<OpenTelemetry.Trace.TracerProviderBuilder>? ConfigureTracing { get; set; }
            }

            public static class QylHostApplicationBuilderExtensions
            {
                public static T AddQyl<T>(this T builder, System.Action<QylTelemetryOptions>? configure = null) => builder;
            }
        }
        """);
    [TestMethod]
    public async Task Reports_a_contrib_instrumentation_extension_next_to_AddQyl_also_inside_ConfigureTracing()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(new Qyl0120ContribInstrumentationWithAddQylAnalyzer(), "QYL0120", """
            using OpenTelemetry.Trace;
            using Qyl;

            public sealed class App
            {
                public static void Configure(App app, TracerProviderBuilder tracing)
                {
                    app.AddQyl(options => options.ConfigureTracing = t => t.AddAspNetCoreInstrumentation());
                    tracing.AddAspNetCoreInstrumentation();
                }
            }
            """, libraries: [OpenTelemetry, AspNetCoreInstrumentation, Hosting], cancellationToken: testContext.CancellationToken);
        Assert.HasCount(2, diagnostics);
        Assert.Contains("AddAspNetCoreInstrumentation", diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
        Assert.Contains("OpenTelemetry.Instrumentation.AspNetCore", diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Is_silent_without_AddQyl_and_for_other_extensions()
    {
        var withoutQyl = await AnalyzerRun.DiagnosticsAsync(new Qyl0120ContribInstrumentationWithAddQylAnalyzer(), "QYL0120", """
            using OpenTelemetry.Trace;

            public static class App
            {
                public static void Configure(TracerProviderBuilder tracing) => tracing.AddAspNetCoreInstrumentation();
            }
            """, libraries: [OpenTelemetry, AspNetCoreInstrumentation, Hosting], cancellationToken: testContext.CancellationToken);
        var otherExtension = await AnalyzerRun.DiagnosticsAsync(new Qyl0120ContribInstrumentationWithAddQylAnalyzer(), "QYL0120", """
            using Contoso;
            using OpenTelemetry.Trace;
            using Qyl;

            public sealed class App
            {
                public static void Configure(App app, TracerProviderBuilder tracing)
                {
                    app.AddQyl();
                    tracing.AddContoso().AddSource("Contoso.Jobs");
                }
            }
            """, libraries: [OpenTelemetry, ContosoExtensions, Hosting], cancellationToken: testContext.CancellationToken);
        Assert.IsEmpty(withoutQyl);
        Assert.IsEmpty(otherExtension);
    }
}
