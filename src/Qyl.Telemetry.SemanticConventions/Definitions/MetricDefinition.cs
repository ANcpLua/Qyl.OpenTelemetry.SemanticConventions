namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A semantic-convention metric as a first-class object: the canonical name is a property,
/// and the instrument, unit, entity references, attribute references, stability, brief, and
/// structured deprecation travel with it. Generic over the instrument marker type for
/// compile-time instrument safety.
/// </summary>
/// <typeparam name="TInstrument">The instrument marker (<see cref="Counter"/>, <see cref="UpDownCounter"/>, <see cref="Gauge"/>, or <see cref="Histogram"/>).</typeparam>
public sealed class MetricDefinition<TInstrument>
    where TInstrument : struct, IInstrument
{
    /// <summary>Initializes a new metric definition.</summary>
    /// <param name="name">The canonical OpenTelemetry metric name.</param>
    /// <param name="unit">The unit, verbatim from the registry (e.g. <c>{thread}</c>, <c>1</c>).</param>
    /// <param name="brief">Human-readable brief.</param>
    /// <param name="stability">The stability tier.</param>
    /// <param name="deprecation">Structured deprecation; <see cref="Deprecation.None"/> when current.</param>
    /// <param name="entities">Entities this metric is associated with.</param>
    /// <param name="attributes">Attributes this metric carries, with requirement levels.</param>
    public MetricDefinition(
        string name,
        string unit,
        string brief,
        Stability stability,
        Deprecation deprecation,
        IReadOnlyList<EntityRef> entities,
        IReadOnlyList<AttributeRef> attributes)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        Brief = brief ?? throw new ArgumentNullException(nameof(brief));
        Stability = stability;
        Deprecation = deprecation ?? throw new ArgumentNullException(nameof(deprecation));
        Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>The canonical OpenTelemetry metric name.</summary>
    public string Name { get; }

    /// <summary>The instrument kind (e.g. <c>counter</c>), from the marker type.</summary>
    public string Instrument => default(TInstrument).Kind;

    /// <summary>The unit, verbatim from the registry (e.g. <c>{thread}</c>, <c>1</c>).</summary>
    public string Unit { get; }

    /// <summary>Human-readable brief.</summary>
    public string Brief { get; }

    /// <summary>The stability tier.</summary>
    public Stability Stability { get; }

    /// <summary>Structured deprecation; <see cref="Deprecation.None"/> when current.</summary>
    public Deprecation Deprecation { get; }

    /// <summary>Entities this metric is associated with (e.g. <c>process</c>, <c>host</c>).</summary>
    public IReadOnlyList<EntityRef> Entities { get; }

    /// <summary>Attributes this metric carries, with requirement levels.</summary>
    public IReadOnlyList<AttributeRef> Attributes { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
