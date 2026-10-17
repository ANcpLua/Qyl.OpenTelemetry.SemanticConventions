namespace Qyl.Telemetry.SemanticConventions;

/// <summary>A producer's reference to an entity described by resource attributes.</summary>
[JetBrains.Annotations.PublicAPI]
public sealed class ResourceEntityReference
{
    /// <summary>Initializes an entity reference.</summary>
    /// <param name="type">The entity type.</param>
    /// <param name="identityKeys">Resource attribute keys that identify the entity.</param>
    public ResourceEntityReference(string type, IReadOnlyList<string> identityKeys)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        IdentityKeys = identityKeys ?? throw new ArgumentNullException(nameof(identityKeys));
    }

    /// <summary>The entity type supplied by the producer.</summary>
    public string Type { get; }

    /// <summary>The resource attribute keys that identify the entity.</summary>
    public IReadOnlyList<string> IdentityKeys { get; }

    /// <summary>The resource attribute keys that describe the entity.</summary>
    public IReadOnlyList<string> DescriptionKeys { get; init; } = Array.Empty<string>();

    /// <summary>The schema for the entity and the referenced attributes.</summary>
    public string? SchemaUrl { get; init; }
}
