namespace Qyl.Telemetry.SemanticConventions;

/// <summary>The resource attached to a telemetry signal.</summary>
[JetBrains.Annotations.PublicAPI]
public sealed class ResourceRecord
{
    /// <summary>Initializes a resource from its original typed attributes.</summary>
    /// <param name="attributes">The resource attributes.</param>
    public ResourceRecord(IReadOnlyDictionary<string, object?> attributes) =>
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

    /// <summary>The original resource attributes.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; }

    /// <summary>The schema URL for the resource, independent of the instrumentation scope.</summary>
    public string? SchemaUrl { get; init; }

    /// <summary>The resource attributes dropped before export.</summary>
    public uint DroppedAttributesCount { get; init; }

    /// <summary>The entities described by this resource and the attribute keys that identify them.</summary>
    public IReadOnlyList<ResourceEntityReference> EntityReferences { get; init; } = Array.Empty<ResourceEntityReference>();
}
