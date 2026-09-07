using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Qyl.Telemetry.SemanticConventions.Incubating.Mapping;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Cross-verifies the collector's normalize table against the registry's own deprecations.
/// </summary>
/// <remarks>
/// <see cref="AttributeMapping"/> and <c>SemconvDeprecations</c> are generated from the same
/// registry by two different Weaver templates through two different JQ filters, so agreeing
/// is not a tautology: a filter that drifts shows up here. The invariant the qyl collector
/// relies on is that a deprecated key falls into exactly one of two buckets — it either names
/// a replacement, and <see cref="AttributeMapping.TryGetRename"/> resolves it transitively to
/// the final live key, or it does not, and <see cref="AttributeMapping.IsObsoleted"/> tells
/// the collector to drop it and count the drop.
/// </remarks>
public sealed class AttributeMappingTests
{
    [Fact]
    public void IsObsoleted_Is_Exactly_The_Registry_Deprecations_Without_A_Replacement()
    {
        var problems = new List<string>();

        foreach (var key in SemconvDeprecations.DeprecatedWithoutReplacement)
            if (!AttributeMapping.IsObsoleted(key))
                problems.Add($"MISSING: registry deprecates '{key}' with no replacement, IsObsoleted says false");

        foreach (var (key, renamedTo) in SemconvDeprecations.Renamed)
            if (AttributeMapping.IsObsoleted(key))
                problems.Add($"EXTRA: registry renames '{key}' to '{renamedTo}', IsObsoleted says true");

        problems.Should().BeEmpty(
            "IsObsoleted must be exactly the registry's deprecations that name no replacement:\n{0}",
            string.Join("\n", problems.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void Obsoleted_And_Renamed_Are_Disjoint()
    {
        var problems = SemconvDeprecations.Renamed
            .Select(entry => entry.Key)
            .Concat(SemconvDeprecations.DeprecatedWithoutReplacement)
            .Where(key => AttributeMapping.IsObsoleted(key) && AttributeMapping.TryGetRename(key, out _))
            .Select(key => $"BOTH: '{key}' is obsoleted and renamed; the collector would drop and rewrite it")
            .Order(StringComparer.Ordinal)
            .ToList();

        problems.Should().BeEmpty(
            "a deprecated key either names a replacement or does not, never both:\n{0}",
            string.Join("\n", problems));
    }

    [Fact]
    public void A_Live_Key_Is_Neither_Obsoleted_Nor_Renamed()
    {
        AttributeMapping.IsObsoleted("db.system.name").Should().BeFalse();
        AttributeMapping.TryGetRename("db.system.name", out _).Should().BeFalse();

        AttributeMapping.IsObsoleted("qyl.instrumentation.domain").Should().BeFalse();
        AttributeMapping.TryGetRename("qyl.instrumentation.domain", out _).Should().BeFalse();
    }

    [Fact]
    public void Exception_Escaped_Is_Obsoleted()
    {
        // The one obsoleted key the first AutoInstrumentation live-check run actually saw:
        // NServiceBus emits it, upstream removed it with no replacement, and the collector
        // has to drop it rather than forward or rewrite it.
        AttributeMapping.IsObsoleted("exception.escaped").Should().BeTrue();
        AttributeMapping.TryGetRename("exception.escaped", out _).Should().BeFalse();
    }

    [Fact]
    public void A_Vendor_Key_Passes_Through_Untouched()
    {
        foreach (var key in new[] { "soap.message_version", "wcf.channel.path", "az.schema_url", "db.elasticsearch.schema_url" })
        {
            AttributeMapping.IsVendorPassThrough(key).Should().BeTrue();
            AttributeMapping.IsObsoleted(key).Should().BeFalse();
            AttributeMapping.TryGetRename(key, out _).Should().BeFalse();
        }
    }
}
