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

internal enum SemconvLegacyMode
{
    Production,
    Compatibility,
    Off,
}

internal readonly struct SemconvMigrationCatalogEntry
{
    public SemconvMigrationCatalogEntry(
        string oldName,
        SemconvMigrationItemKind kind,
        string signal,
        string domain,
        string sinceVersion,
        ImmutableArray<string> replacementNames,
        SemconvMigrationKind migrationKind,
        string changelogVersion,
        string changelogEvidence,
        DiagnosticSeverity defaultProductionSeverity,
        DiagnosticSeverity fixtureSeverity)
        : this(
            oldName,
            kind,
            signal,
            domain,
            sinceVersion,
            replacementNames,
            migrationKind,
            changelogVersion,
            changelogEvidence,
            defaultProductionSeverity,
            fixtureSeverity,
            evidence: SemconvChangelogEvidence.None)
    {
    }

    public SemconvMigrationCatalogEntry(
        string oldName,
        SemconvMigrationItemKind kind,
        string signal,
        string domain,
        string sinceVersion,
        ImmutableArray<string> replacementNames,
        SemconvMigrationKind migrationKind,
        string changelogVersion,
        string changelogEvidence,
        DiagnosticSeverity defaultProductionSeverity,
        DiagnosticSeverity fixtureSeverity,
        SemconvChangelogEvidence evidence)
    {
        OldName = oldName;
        Kind = kind;
        Signal = signal;
        Domain = domain;
        SinceVersion = sinceVersion;
        ReplacementNames = replacementNames;
        MigrationKind = migrationKind;
        ChangelogVersion = changelogVersion;
        ChangelogEvidence = changelogEvidence;
        DefaultProductionSeverity = defaultProductionSeverity;
        FixtureSeverity = fixtureSeverity;
        Evidence = evidence;
    }

    public string OldName { get; }

    public SemconvMigrationItemKind Kind { get; }

    public string Signal { get; }

    public string Domain { get; }

    public string SinceVersion { get; }

    public ImmutableArray<string> ReplacementNames { get; }

    public SemconvMigrationKind MigrationKind { get; }

    public string ChangelogVersion { get; }

    /// <summary>
    /// Human-readable summary surfaced in docs and diagnostic messages.
    /// For the machine-checkable origin (commit SHA + permalink + raw quote)
    /// see <see cref="Evidence"/>.
    /// </summary>
    public string ChangelogEvidence { get; }

    /// <summary>
    /// Structured provenance (commit / version / url / quote). When
    /// <see cref="SemconvChangelogEvidence.IsPresent"/> is true, the docs
    /// generator renders it as a hyperlink; auditors can verify the catalog
    /// claim against the pinned upstream commit without re-parsing the
    /// CHANGELOG.md by hand. Optional for back-compat with older catalog
    /// entries — older entries default to <see cref="SemconvChangelogEvidence.None"/>.
    /// </summary>
    public SemconvChangelogEvidence Evidence { get; }

    public DiagnosticSeverity DefaultProductionSeverity { get; }

    public DiagnosticSeverity FixtureSeverity { get; }

    public bool HasExactReplacement =>
        (MigrationKind == SemconvMigrationKind.ExactRename
            || MigrationKind == SemconvMigrationKind.ExactValueRename)
        && ReplacementNames.Length == 1
        && !string.IsNullOrWhiteSpace(ReplacementNames[0]);
}
