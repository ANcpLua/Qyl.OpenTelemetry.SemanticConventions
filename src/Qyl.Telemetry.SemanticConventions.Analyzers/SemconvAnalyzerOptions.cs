// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
/// Reads MSBuild-property-driven analyzer options out of
/// <see cref="AnalyzerConfigOptions"/>. Centralises the property names as the
/// single source of truth for implementation and tests.
/// </summary>
internal static class SemconvAnalyzerOptions
{
    /// <summary>
    /// <c>build_property.OtelSemConvNonAttributesTiers</c> — when <c>true</c>,
    /// extends the deprecation-detecting analyzers beyond the conventional
    /// <c>*Attributes</c> classes to also recognise the three non-Attributes
    /// tiers Weaver SourceGeneration emits (<c>*Metrics</c>, <c>*Meters</c>,
    /// <c>*Activities</c>). Default <c>false</c> scans only
    /// <c>*Attributes</c> classes.
    /// </summary>
    public const string NonAttributesTiersBuildProperty = "build_property.OtelSemConvNonAttributesTiers";

    public static bool ShouldAllowNonAttributesTiers(AnalyzerConfigOptions options) =>
        options.TryGetValue(NonAttributesTiersBuildProperty, out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
