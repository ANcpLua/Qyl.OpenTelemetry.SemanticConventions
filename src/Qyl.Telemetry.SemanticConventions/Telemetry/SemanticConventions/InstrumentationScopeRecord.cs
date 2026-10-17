namespace Qyl.Telemetry.SemanticConventions;

/// <summary>The producer's instrumentation scope, shared by traces, metrics and logs.</summary>
[JetBrains.Annotations.PublicAPI]
public sealed class InstrumentationScopeRecord
{
    /// <summary>The scope name, when supplied.</summary>
    public string? Name { get; init; }

    /// <summary>The producer's version string; it need not be a semantic version.</summary>
    public string? Version { get; init; }

    /// <summary>The schema URL for this scope.</summary>
    public string? SchemaUrl { get; init; }

    /// <summary>The original typed scope attributes.</summary>
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();

    /// <summary>The scope attributes dropped before export.</summary>
    public uint DroppedAttributesCount { get; init; }
}
