namespace Qyl.Telemetry.SemanticConventions;

/// <summary>When an attribute's value becomes known, from the registry annotation <c>qyl.phase</c>.</summary>
public enum AttributePhase
{
    /// <summary>The value is known when the operation starts; a span starter takes it as a parameter.</summary>
    Start,

    /// <summary>
    /// The value is known only when the operation ends (<c>error.type</c>, <c>*.response.*</c>); it is set
    /// through the typed setters or <c>RecordFailure</c> before the span ends, never at creation.
    /// </summary>
    End,
}
