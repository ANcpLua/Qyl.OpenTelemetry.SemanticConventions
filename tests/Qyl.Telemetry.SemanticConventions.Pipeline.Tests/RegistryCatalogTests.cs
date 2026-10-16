using System.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Incubating.Activities;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class RegistryCatalogTests
{
    [TestMethod]
    public void Oracle_duration_retains_the_vendor_wire_representation_without_a_numeric_unit()
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute("db.odp.roundtrip.duration", out var attribute));
        Assert.IsNotNull(attribute);
        Assert.AreEqual(RegistryAttributeValueKind.String, attribute.ValueKind);
        using var span = new Activity("oracle.duration");
        const string observed = "00:00:00.0595180";
        Assert.AreSame(span, span.SetDbOdpRoundtripDuration(observed));
        Assert.AreEqual(observed, span.GetTagItem("db.odp.roundtrip.duration"));
    }

    [TestMethod]
    [DataRow("service.name", RegistryAttributeValueKind.String)]
    [DataRow("http.response.status_code", RegistryAttributeValueKind.Integer)]
    [DataRow("app.jank.period", RegistryAttributeValueKind.Double)]
    [DataRow("aspnetcore.request.is_unhandled", RegistryAttributeValueKind.Boolean)]
    [DataRow("http.request.header.content_type", RegistryAttributeValueKind.StringArray)]
    [DataRow("pprof.scope.sample_type_order", RegistryAttributeValueKind.IntegerArray)]
    [DataRow("feature_flag.result.value", RegistryAttributeValueKind.Any)]
    public void Looks_up_stable_incubating_and_template_value_kinds(string key, RegistryAttributeValueKind kind)
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute(key, out var attribute));
        Assert.IsNotNull(attribute);
        Assert.AreEqual(kind, attribute.ValueKind);
    }

    [TestMethod]
    public void Templates_require_a_nonempty_suffix_and_do_not_change_the_registry_key()
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute("http.request.header.content_type", out var header));
        Assert.IsNotNull(header);
        Assert.AreEqual("http.request.header", header.Name);
        Assert.IsTrue(header.IsTemplate);
        Assert.AreEqual(Stability.Stable, header.Stability);
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("http.request.header", out _));
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("http.request.header.", out _));
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("http.request.headers.content_type", out _));
    }

    [TestMethod]
    public void Deprecation_distinguishes_renames_from_removal_without_inventing_a_replacement()
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute("db.system", out var database));
        Assert.IsNotNull(database);
        Assert.AreEqual(MigrationKind.Renamed, database.Deprecation.Kind);
        Assert.AreEqual("db.system.name", database.Deprecation.Replacement);
        Assert.IsTrue(RegistryCatalog.TryGetAttribute("exception.escaped", out var escaped));
        Assert.IsNotNull(escaped);
        Assert.AreEqual(MigrationKind.Obsoleted, escaped.Deprecation.Kind);
        Assert.IsNull(escaped.Deprecation.Replacement);
    }

    [TestMethod]
    public void Metric_metadata_preserves_instrument_unit_stability_and_exact_rename()
    {
        Assert.IsTrue(RegistryCatalog.TryGetMetric("http.server.request.duration", out var http));
        Assert.IsNotNull(http);
        Assert.AreEqual("histogram", http.Instrument);
        Assert.AreEqual("s", http.Unit);
        Assert.AreEqual(Stability.Stable, http.Stability);
        Assert.IsFalse(http.Deprecation.IsDeprecated);
        Assert.IsTrue(RegistryCatalog.TryGetMetric("system.cpu.time", out var cpu));
        Assert.IsNotNull(cpu);
        Assert.AreEqual("counter", cpu.Instrument);
        Assert.AreEqual("s", cpu.Unit);
        Assert.AreEqual(Stability.Development, cpu.Stability);
        Assert.IsTrue(RegistryCatalog.TryGetMetric("cpu.time", out var old));
        Assert.IsNotNull(old);
        Assert.AreEqual("system.cpu.time", old.Deprecation.Replacement);
    }

    [TestMethod]
    public void Uncategorized_deprecation_notes_preserve_registry_newlines()
    {
        Assert.IsTrue(RegistryCatalog.TryGetAttribute("code.function", out var function));
        Assert.IsNotNull(function);
        Assert.AreEqual(MigrationKind.Uncategorized, function.Deprecation.Kind);
        Assert.AreEqual("Value should be included in `code.function.name` which is expected to be a fully-qualified name.\n",
            function.Deprecation.Note);
    }

    [TestMethod]
    public void Unregistered_domain_names_and_different_casing_are_unknown()
    {
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("shop.order.id", out var attribute));
        Assert.IsNull(attribute);
        Assert.IsFalse(RegistryCatalog.TryGetMetric("shop.order.total", out var metric));
        Assert.IsNull(metric);
        Assert.IsFalse(RegistryCatalog.TryGetAttribute("Service.Name", out _));
        Assert.IsFalse(RegistryCatalog.TryGetMetric("HTTP.server.request.duration", out _));
    }
}
