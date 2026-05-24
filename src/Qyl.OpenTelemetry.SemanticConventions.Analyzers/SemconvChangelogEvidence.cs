// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Structured provenance for a single curated catalog entry. Pins each migration
/// claim to a specific commit/version/URL/quote in the upstream
/// open-telemetry/semantic-conventions repository so the catalog can be audited
/// without re-parsing CHANGELOG.md by hand.
///
/// Backfills the older free-text <c>ChangelogEvidence</c> field on
/// <see cref="SemconvMigrationCatalogEntry"/>. Both shapes coexist: <c>ChangelogEvidence</c>
/// remains the human-readable summary surfaced in docs and diagnostic messages,
/// while <see cref="SemconvChangelogEvidence"/> is the machine-checkable origin
/// (commit SHA + permalink + raw quote) that the docs generator renders as a
/// hyperlink and the seeding script always emits.
/// </summary>
internal readonly struct SemconvChangelogEvidence : IEquatable<SemconvChangelogEvidence>
{
    public SemconvChangelogEvidence(string commit, string version, string url, string quote)
    {
        Commit = commit ?? string.Empty;
        Version = version ?? string.Empty;
        Url = url ?? string.Empty;
        Quote = quote ?? string.Empty;
    }

    /// <summary>40-char upstream semantic-conventions commit SHA the evidence is pinned to.</summary>
    public string Commit { get; }

    /// <summary>Semantic-conventions version tag (without the leading 'v'), e.g. <c>1.41.0</c>.</summary>
    public string Version { get; }

    /// <summary>Permalink into CHANGELOG.md or the relevant model YAML at the pinned commit.</summary>
    public string Url { get; }

    /// <summary>Raw quote from the source line, preserved verbatim for audit.</summary>
    public string Quote { get; }

    /// <summary>True when the evidence carries enough to be auditable (commit + url + quote).</summary>
    public bool IsPresent =>
        !string.IsNullOrEmpty(Commit)
        && !string.IsNullOrEmpty(Url)
        && !string.IsNullOrEmpty(Quote);

    public bool Equals(SemconvChangelogEvidence other) =>
        string.Equals(Commit, other.Commit, StringComparison.Ordinal)
        && string.Equals(Version, other.Version, StringComparison.Ordinal)
        && string.Equals(Url, other.Url, StringComparison.Ordinal)
        && string.Equals(Quote, other.Quote, StringComparison.Ordinal);

    public override bool Equals(object? obj) =>
        obj is SemconvChangelogEvidence other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Commit);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Version);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Url);
            hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Quote);
            return hash;
        }
    }

    public static SemconvChangelogEvidence None => default;
}
