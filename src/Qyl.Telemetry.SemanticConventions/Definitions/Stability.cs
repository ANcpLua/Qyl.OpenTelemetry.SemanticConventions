namespace Qyl.Telemetry.SemanticConventions;

/// <summary>Stability tier of a semantic convention, as declared by the upstream registry.</summary>
public enum Stability
{
    /// <summary>The convention is stable: its name and shape are frozen.</summary>
    Stable,

    /// <summary>The convention is a release candidate for stabilization.</summary>
    ReleaseCandidate,

    /// <summary>The convention is in beta.</summary>
    Beta,

    /// <summary>The convention is in alpha.</summary>
    Alpha,

    /// <summary>The convention is under development and may change between registry releases.</summary>
    Development,

    /// <summary>The convention is deprecated; see its <see cref="Deprecation"/> for the migration.</summary>
    Deprecated,
}
