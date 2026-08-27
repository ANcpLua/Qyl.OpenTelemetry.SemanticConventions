namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A semantic-convention entity as a first-class object: its type name, stability, brief,
/// structured deprecation, and the full attribute references that describe and identify it
/// (with requirement levels). This is the resolved entity definition, distinct from the
/// name-only <see cref="EntityRef"/> that a metric or span carries in its association list.
/// </summary>
public sealed class EntityDefinition
{
    /// <summary>Initializes a new entity definition.</summary>
    /// <param name="name">The entity type name (e.g. <c>host</c>, <c>process</c>).</param>
    /// <param name="brief">Human-readable brief.</param>
    /// <param name="stability">The stability tier.</param>
    /// <param name="deprecation">Structured deprecation; <see cref="Deprecation.None"/> when current.</param>
    /// <param name="attributes">The attributes that describe and identify this entity.</param>
    public EntityDefinition(
        string name,
        string brief,
        Stability stability,
        Deprecation deprecation,
        IReadOnlyList<AttributeRef> attributes)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Brief = brief ?? throw new ArgumentNullException(nameof(brief));
        Stability = stability;
        Deprecation = deprecation ?? throw new ArgumentNullException(nameof(deprecation));
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    /// <summary>The entity type name (e.g. <c>host</c>, <c>process</c>).</summary>
    public string Name { get; }

    /// <summary>Human-readable brief.</summary>
    public string Brief { get; }

    /// <summary>The stability tier.</summary>
    public Stability Stability { get; }

    /// <summary>Structured deprecation; <see cref="Deprecation.None"/> when current.</summary>
    public Deprecation Deprecation { get; }

    /// <summary>The attributes that describe and identify this entity.</summary>
    public IReadOnlyList<AttributeRef> Attributes { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
