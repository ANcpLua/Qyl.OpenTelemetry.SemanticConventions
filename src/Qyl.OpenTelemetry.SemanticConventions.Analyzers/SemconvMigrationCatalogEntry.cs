// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

internal enum SemconvMigrationItemKind
{
    AttributeKey,
    AttributeValue,
    MetricName,
    EventName,
    SpanName,
    ResourceAttribute,
    EnumValue,
    Namespace,
    Group,
    GuidanceOnly,
}

internal enum SemconvMigrationKind
{
    ExactRename,
    ExactValueRename,
    RemovedNoReplacement,
    ContextSensitive,
    ManualReview,
    DeprecatedButGenerated,
}

internal readonly struct SemconvMigrationCatalogEntry
{
    public SemconvMigrationCatalogEntry(
        string oldName,
        SemconvMigrationItemKind kind,
        string signal,
        string domain,
        string version,
        ImmutableArray<string> replacementNames,
        SemconvMigrationKind migrationKind,
        string changelogEvidence)
    {
        OldName = oldName;
        Kind = kind;
        Signal = signal;
        Domain = domain;
        Version = version;
        ReplacementNames = replacementNames;
        MigrationKind = migrationKind;
        ChangelogEvidence = changelogEvidence;
    }

    public string OldName { get; }

    public SemconvMigrationItemKind Kind { get; }

    public string Signal { get; }

    public string Domain { get; }

    /// <summary>
    /// Semantic-conventions version the deprecation landed in (without the
    /// leading 'v'), or empty when the upstream model carries no version.
    /// </summary>
    public string Version { get; }

    public ImmutableArray<string> ReplacementNames { get; }

    public SemconvMigrationKind MigrationKind { get; }

    /// <summary>
    /// Human-readable summary surfaced in docs and diagnostic messages.
    /// </summary>
    public string ChangelogEvidence { get; }

    public bool HasExactReplacement =>
        (MigrationKind == SemconvMigrationKind.ExactRename
            || MigrationKind == SemconvMigrationKind.ExactValueRename
            || MigrationKind == SemconvMigrationKind.DeprecatedButGenerated)
        && ReplacementNames.Length == 1
        && !string.IsNullOrWhiteSpace(ReplacementNames[0]);
}
