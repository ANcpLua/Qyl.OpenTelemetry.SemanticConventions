namespace Qyl.Telemetry.SemanticConventions;

/// <summary>The OTLP span kind, including an explicitly unspecified kind.</summary>
[JetBrains.Annotations.PublicAPI]
public enum SpanKind
{
    /// <summary>The producer did not specify a kind.</summary>
    Unspecified = 0,
    /// <summary>An internal operation.</summary>
    Internal = 1,
    /// <summary>A server operation.</summary>
    Server = 2,
    /// <summary>A client operation.</summary>
    Client = 3,
    /// <summary>A message producer operation.</summary>
    Producer = 4,
    /// <summary>A message consumer operation.</summary>
    Consumer = 5,
}
