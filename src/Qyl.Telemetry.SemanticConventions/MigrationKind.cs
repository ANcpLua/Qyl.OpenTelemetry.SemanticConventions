#if QYL_ANALYZER_CATALOG
namespace Qyl.Telemetry.SemanticConventions.Analyzers;
#else
namespace Qyl.Telemetry.SemanticConventions;
#endif

/// <summary>The kind of change that deprecated a semantic convention.</summary>
#if QYL_ANALYZER_CATALOG
internal
#else
[JetBrains.Annotations.PublicAPI]
public
#endif
enum MigrationKind
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
