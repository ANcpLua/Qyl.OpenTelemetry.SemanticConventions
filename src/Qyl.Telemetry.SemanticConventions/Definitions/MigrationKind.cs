namespace Qyl.Telemetry.SemanticConventions;

/// <summary>The kind of change that deprecated a semantic convention.</summary>
public enum MigrationKind
{
    /// <summary>The convention is current; not deprecated.</summary>
    None,

    /// <summary>The convention was renamed to an exact successor.</summary>
    Renamed,

    /// <summary>The convention was removed with no replacement.</summary>
    Obsoleted,

    /// <summary>The convention was deprecated for a reason upstream did not categorize.</summary>
    Uncategorized,
}
