using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class AnalyzerCatalogProjectionTests
{
    [TestMethod]
    [DataRow("service.name")]
    [DataRow("http.response.status_code")]
    [DataRow("app.jank.period")]
    [DataRow("aspnetcore.request.is_unhandled")]
    [DataRow("http.request.header.content_type")]
    [DataRow("pprof.scope.sample_type_order")]
    [DataRow("feature_flag.result.value")]
    public void Compiler_lookup_preserves_public_value_kinds_and_template_expansion(string key)
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute(key, out var published));
        Assert.IsNotNull(published);
        Assert.IsTrue(SemconvRegistryFacts.TryGetAttributeType(key, out var compiled));
        Assert.AreEqual(published.ValueKind.ToString(), compiled.ToString());
        Assert.AreEqual(!published.IsTemplate, SemconvRegistryFacts.IsRegistryAttributeKey(key));
    }

    [TestMethod]
    [DataRow("http.request.header")]
    [DataRow("http.request.header.")]
    [DataRow("Service.Name")]
    [DataRow("shop.order.id")]
    public void Compiler_lookup_does_not_invent_registry_keys(string key)
    {
        Assert.IsFalse(SemconvRegistryFacts.IsKnownAttributeKey(key));
        Assert.IsFalse(SemconvRegistryFacts.IsRegistryAttributeKey(key));
    }

    [TestMethod]
    [DataRow("http.server.request.duration", true)]
    [DataRow("cpu.time", true)]
    [DataRow("HTTP.server.request.duration", false)]
    [DataRow("shop.order.total", false)]
    public void Compiler_metric_names_preserve_registered_and_deprecated_names(string name, bool expected)
    {
        Assert.AreEqual(expected, RegistryCatalog.TryGetMetric(name, out _));
        Assert.AreEqual(expected, SemconvRegistryFacts.IsKnownMetricName(name));
    }
}
