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

    /// <summary>
    /// <c>build_property.OtelSemConvInstrumentationLibrary</c> — when <c>true</c>,
    /// QYL0008 and QYL0101 do not report in that project. Set it in instrumentation libraries
    /// that intentionally ship against the incubating tier and version-lock with it
    /// (for example <c>Qyl.Telemetry.AutoInstrumentation</c>), so the rule stays on
    /// for ordinary libraries that must not push that volatility downstream.
    /// </summary>
    public const string InstrumentationLibraryBuildProperty = "build_property.OtelSemConvInstrumentationLibrary";

    public static bool IsInstrumentationLibrary(AnalyzerConfigOptions options) =>
        options.TryGetValue(InstrumentationLibraryBuildProperty, out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
