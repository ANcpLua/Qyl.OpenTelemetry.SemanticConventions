// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

internal enum SemconvMigrationItemKind
{
    AttributeKey,
    AttributeValue,
    MetricName,
    EventName,
    SpanName,
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

internal readonly struct SemconvMigrationCatalogEntry(
        string oldName,
        SemconvMigrationItemKind kind,
        string signal,
        string domain,
        string version,
        ImmutableArray<string> replacementNames,
        SemconvMigrationKind migrationKind,
        string changelogEvidence) : IEquatable<SemconvMigrationCatalogEntry>
{

    public string OldName { get; } = oldName;

    public SemconvMigrationItemKind Kind { get; } = kind;

    public string Signal { get; } = signal;

    public string Domain { get; } = domain;

    /// <summary>
    /// Semantic-conventions version the deprecation landed in (without the
    /// leading 'v'), or empty when the upstream model carries no version.
    /// </summary>
    public string Version { get; } = version;

    public ImmutableArray<string> ReplacementNames { get; } = replacementNames;

    public SemconvMigrationKind MigrationKind { get; } = migrationKind;

    /// <summary>
    /// Human-readable summary surfaced in docs and diagnostic messages.
    /// </summary>
    public string ChangelogEvidence { get; } = changelogEvidence;

    public bool HasExactReplacement =>
        MigrationKind is SemconvMigrationKind.ExactRename or SemconvMigrationKind.ExactValueRename or SemconvMigrationKind.DeprecatedButGenerated
        && ReplacementNames.Length == 1
        && !string.IsNullOrWhiteSpace(ReplacementNames[0]);

    public bool Equals(SemconvMigrationCatalogEntry other) =>
        OldName == other.OldName && Kind == other.Kind && Signal == other.Signal
        && Domain == other.Domain && Version == other.Version && MigrationKind == other.MigrationKind
        && ChangelogEvidence == other.ChangelogEvidence
        && ReplacementNames.IsDefault == other.ReplacementNames.IsDefault
        && (ReplacementNames.IsDefault || ReplacementNames.SequenceEqual(other.ReplacementNames));

    public override bool Equals(object? obj) => obj is SemconvMigrationCatalogEntry other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = OldName is null ? 0 : StringComparer.Ordinal.GetHashCode(OldName);
            hash = (hash * 397) ^ Kind.GetHashCode();
            hash = (hash * 397) ^ (Signal is null ? 0 : StringComparer.Ordinal.GetHashCode(Signal));
            hash = (hash * 397) ^ (Domain is null ? 0 : StringComparer.Ordinal.GetHashCode(Domain));
            hash = (hash * 397) ^ (Version is null ? 0 : StringComparer.Ordinal.GetHashCode(Version));
            hash = (hash * 397) ^ MigrationKind.GetHashCode();
            hash = (hash * 397) ^ (ChangelogEvidence is null ? 0 : StringComparer.Ordinal.GetHashCode(ChangelogEvidence));
            if (!ReplacementNames.IsDefault)
                foreach (var name in ReplacementNames)
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(name);
            return hash;
        }
    }

    public static bool operator ==(SemconvMigrationCatalogEntry left, SemconvMigrationCatalogEntry right) => left.Equals(right);
    public static bool operator !=(SemconvMigrationCatalogEntry left, SemconvMigrationCatalogEntry right) => !left.Equals(right);

}
