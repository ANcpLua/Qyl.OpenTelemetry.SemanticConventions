using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class ApplicationAttributeTests
{
    [TestMethod]
    public void Application_attributes_export_with_their_original_names_and_types()
    {
        using var source = new ActivitySource("ApplicationAttributeTests");
        using var exporter = new AttributeExporter();
        using var provider = Sdk.CreateTracerProviderBuilder().AddSource(source.Name)
            .AddProcessor(new SimpleActivityExportProcessor(exporter)).Build();
        string[] labels = ["first", "second"];
        using (var span = source.StartActivity("checkout"))
        {
            Assert.IsNotNull(span);
            span.SetTag("checkout.sequence", 9_007_199_254_740_993L);
            span.SetTag("checkout.accepted", false);
            span.SetTag("checkout.total", 41.5);
            span.SetTag("checkout.labels", labels);
            span.SetTag("checkout.empty", string.Empty);
        }

        Assert.AreEqual(9_007_199_254_740_993L, exporter.Attributes["checkout.sequence"]);
        Assert.AreEqual(false, exporter.Attributes["checkout.accepted"]);
        Assert.AreEqual(41.5, exporter.Attributes["checkout.total"]);
        Assert.AreSequenceEqual(["first", "second"], (string[])exporter.Attributes["checkout.labels"]!);
        Assert.AreEqual(string.Empty, exporter.Attributes["checkout.empty"]);
        Assert.IsFalse(exporter.Attributes.Keys.Any(static key => key.StartsWith("qyl.app.", StringComparison.Ordinal)));
        foreach (var key in exporter.Attributes.Keys)
            Assert.IsFalse(RegistryCatalog.TryGetAttribute(key, out _), "Custom attributes do not acquire a registry type.");
    }

    [TestMethod]
    public void Historical_qyl_app_names_have_no_invented_string_type()
    {
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("qyl.app.sequence", out _));
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("qyl.app.accepted", out _));
    }

    private sealed class AttributeExporter : BaseExporter<Activity>
    {
        internal Dictionary<string, object?> Attributes { get; } = new(StringComparer.Ordinal);

        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var span in batch)
                foreach (var tag in span.TagObjects)
                    Attributes.Add(tag.Key, tag.Value);
            return ExportResult.Success;
        }
    }
}
