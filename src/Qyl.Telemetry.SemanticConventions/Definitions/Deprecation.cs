namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// Structured deprecation projected verbatim from the upstream Weaver model
/// (<c>reason: renamed</c> with <c>renamed_to</c>, <c>obsoleted</c>, or an
/// uncategorized note). <see cref="Replacement"/> is <see langword="null"/> unless the
/// upstream data gives an exact rename target: a deprecation does not imply a successor.
/// </summary>
public sealed class Deprecation
{
    private Deprecation(MigrationKind kind, string? replacement, string? note)
    {
        Kind = kind;
        Replacement = replacement;
        Note = note;
    }

    /// <summary>The convention is current; not deprecated.</summary>
    public static readonly Deprecation None = new(MigrationKind.None, null, null);

    /// <summary>Removed with no replacement at this time.</summary>
    public static readonly Deprecation Obsoleted = new(MigrationKind.Obsoleted, null, null);

    /// <summary>Renamed with an exact 1:1 successor that is safe to auto-map.</summary>
    /// <param name="replacement">The exact successor name.</param>
    /// <returns>A <see cref="MigrationKind.Renamed"/> deprecation.</returns>
    public static Deprecation Renamed(string replacement) =>
        new(MigrationKind.Renamed, replacement ?? throw new ArgumentNullException(nameof(replacement)), null);

    /// <summary>Deprecated for an upstream-uncategorized reason; no machine-readable target.</summary>
    /// <param name="note">The free-text upstream note.</param>
    /// <returns>A <see cref="MigrationKind.Uncategorized"/> deprecation.</returns>
    public static Deprecation Uncategorized(string note) =>
        new(MigrationKind.Uncategorized, null, note ?? throw new ArgumentNullException(nameof(note)));

    /// <summary>The kind of change that deprecated the convention.</summary>
    public MigrationKind Kind { get; }

    /// <summary>The exact rename target, or <see langword="null"/> when none exists (obsoleted or uncategorized).</summary>
    public string? Replacement { get; }

    /// <summary>Free-text upstream note for uncategorized deprecations, else <see langword="null"/>.</summary>
    public string? Note { get; }

    /// <summary><see langword="true"/> unless this is <see cref="None"/>.</summary>
    public bool IsDeprecated => Kind != MigrationKind.None;
}
