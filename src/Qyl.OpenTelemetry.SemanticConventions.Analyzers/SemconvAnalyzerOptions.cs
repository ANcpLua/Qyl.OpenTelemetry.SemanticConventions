// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Reads MSBuild-property-driven analyzer options out of
/// <see cref="AnalyzerConfigOptions"/>. Centralises the property names so a
/// future rename touches one place and tests have a single source of truth.
/// </summary>
internal static class SemconvAnalyzerOptions
{
    /// <summary>
    /// <c>build_property.OtelSemConvNonAttributesTiers</c> — when <c>true</c>,
    /// extends the deprecation-detecting analyzers beyond the conventional
    /// <c>*Attributes</c> classes to also recognise the four non-Attributes
    /// tiers Weaver SourceGeneration emits (<c>*Metrics</c>, <c>*Meters</c>,
    /// <c>*Events</c>, <c>*Activities</c>). Default <c>false</c> preserves
    /// the historic surface: only <c>*Attributes</c> classes are scanned.
    /// </summary>
    public const string NonAttributesTiersBuildProperty = "build_property.OtelSemConvNonAttributesTiers";

    public static bool ShouldAllowNonAttributesTiers(AnalyzerConfigOptions options) =>
        options.TryGetValue(NonAttributesTiersBuildProperty, out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
