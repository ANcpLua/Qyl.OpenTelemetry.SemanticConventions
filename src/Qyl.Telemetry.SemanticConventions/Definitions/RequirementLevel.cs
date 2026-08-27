namespace Qyl.Telemetry.SemanticConventions;

/// <summary>Requirement level of an attribute on a signal (metric, span, event, or entity).</summary>
public enum RequirementLevel
{
    /// <summary>The registry declares no requirement level for the attribute on this signal.</summary>
    Unspecified,

    /// <summary>The attribute must be set.</summary>
    Required,

    /// <summary>The attribute should be set.</summary>
    Recommended,

    /// <summary>The attribute is set only when the consumer opts in.</summary>
    OptIn,

    /// <summary>The attribute must be set when the registry's stated condition holds.</summary>
    ConditionallyRequired,
}
