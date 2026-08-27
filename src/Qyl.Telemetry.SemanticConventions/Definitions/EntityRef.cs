namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A reference to an entity a signal is associated with. Carries the entity type name from
/// the registry; the full <see cref="EntityDefinition"/> (its identifying attributes) lives
/// with the entity conventions.
/// </summary>
public sealed class EntityRef
{
    /// <summary>Initializes a new entity reference.</summary>
    /// <param name="type">The entity type name (e.g. <c>process</c>, <c>host</c>).</param>
    public EntityRef(string type) => Type = type ?? throw new ArgumentNullException(nameof(type));

    /// <summary>The entity type name (e.g. <c>process</c>, <c>host</c>).</summary>
    public string Type { get; }

    /// <inheritdoc/>
    public override string ToString() => Type;
}
