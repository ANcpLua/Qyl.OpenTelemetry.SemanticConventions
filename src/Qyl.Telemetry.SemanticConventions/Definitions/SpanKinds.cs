namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// Marker contract for a span kind. A <see cref="SpanDefinition{TKind}"/> is generic over
/// one of the implementing structs, so an API that only accepts
/// <c>SpanDefinition&lt;Server&gt;</c> rejects a client span at compile time. The kind is an
/// instance property on a stateless struct, resolved through <c>default(TKind).Kind</c>,
/// so the vocabulary needs no static abstract interface members and compiles for
/// <c>netstandard2.0</c>.
/// </summary>
public interface ISpanKind
{
    /// <summary>The registry span kind (<c>client</c>, <c>server</c>, <c>internal</c>, <c>producer</c>, or <c>consumer</c>).</summary>
    string Kind { get; }
}

/// <summary>Marker for a client span.</summary>
public readonly struct Client : ISpanKind
{
    /// <inheritdoc/>
    public string Kind => "client";
}

/// <summary>Marker for a server span.</summary>
public readonly struct Server : ISpanKind
{
    /// <inheritdoc/>
    public string Kind => "server";
}

/// <summary>Marker for an internal span.</summary>
public readonly struct Internal : ISpanKind
{
    /// <inheritdoc/>
    public string Kind => "internal";
}

/// <summary>Marker for a producer span.</summary>
public readonly struct Producer : ISpanKind
{
    /// <inheritdoc/>
    public string Kind => "producer";
}

/// <summary>Marker for a consumer span.</summary>
public readonly struct Consumer : ISpanKind
{
    /// <inheritdoc/>
    public string Kind => "consumer";
}
