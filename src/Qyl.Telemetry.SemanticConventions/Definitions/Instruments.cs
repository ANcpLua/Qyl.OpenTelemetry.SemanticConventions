namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// Marker contract for a metric instrument kind. A <see cref="MetricDefinition{TInstrument}"/>
/// is generic over one of the implementing structs, so an API that only accepts
/// <c>MetricDefinition&lt;Counter&gt;</c> rejects a gauge definition at compile time.
/// The kind is exposed as an instance property on a stateless struct so that
/// <c>default(TInstrument).Kind</c> resolves it without static abstract interface
/// members, which lets the vocabulary compile for <c>netstandard2.0</c>.
/// </summary>
public interface IInstrument
{
    /// <summary>The registry instrument kind (<c>counter</c>, <c>updowncounter</c>, <c>gauge</c>, or <c>histogram</c>).</summary>
    string Kind { get; }
}

/// <summary>Marker for a monotonic counter instrument.</summary>
public readonly struct Counter : IInstrument
{
    /// <inheritdoc/>
    public string Kind => "counter";
}

/// <summary>Marker for an up-down counter instrument.</summary>
public readonly struct UpDownCounter : IInstrument
{
    /// <inheritdoc/>
    public string Kind => "updowncounter";
}

/// <summary>Marker for a gauge instrument.</summary>
public readonly struct Gauge : IInstrument
{
    /// <inheritdoc/>
    public string Kind => "gauge";
}

/// <summary>Marker for a histogram instrument.</summary>
public readonly struct Histogram : IInstrument
{
    /// <inheritdoc/>
    public string Kind => "histogram";
}
