namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A semantic-convention span as a first-class object: its registry id, span kind (as a
/// marker type), stability, brief, structured deprecation, and attribute references (with
/// requirement levels) all travel with it.
/// </summary>
/// <typeparam name="TKind">The span-kind marker (<see cref="Client"/>, <see cref="Server"/>, <see cref="Internal"/>, <see cref="Producer"/>, or <see cref="Consumer"/>).</typeparam>
public sealed class SpanDefinition<TKind>
    where TKind : struct, ISpanKind
{
    /// <summary>Initializes a new span definition.</summary>
    /// <param name="id">The span group id (e.g. <c>http.client</c>).</param>
    /// <param name="brief">Human-readable brief.</param>
    /// <param name="stability">The stability tier.</param>
    /// <param name="deprecation">Structured deprecation; <see cref="Deprecation.None"/> when current.</param>
    /// <param name="attributes">Attributes this span carries, with requirement levels.</param>
    public SpanDefinition(
        string id,
        string brief,
        Stability stability,
        Deprecation deprecation,
        IReadOnlyList<AttributeRef> attributes)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Brief = brief ?? throw new ArgumentNullException(nameof(brief));
        Stability = stability;
        Deprecation = deprecation ?? throw new ArgumentNullException(nameof(deprecation));
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>The span group id (e.g. <c>http.client</c>).</summary>
    public string Id { get; }

    /// <summary>The span kind (e.g. <c>client</c>), from the marker type.</summary>
    public string SpanKind => default(TKind).Kind;

    /// <summary>Human-readable brief.</summary>
    public string Brief { get; }

    /// <summary>The stability tier.</summary>
    public Stability Stability { get; }

    /// <summary>Structured deprecation; <see cref="Deprecation.None"/> when current.</summary>
    public Deprecation Deprecation { get; }

    /// <summary>Attributes this span carries, with requirement levels.</summary>
    public IReadOnlyList<AttributeRef> Attributes { get; }

    /// <inheritdoc/>
    public override string ToString() => Id;
}
