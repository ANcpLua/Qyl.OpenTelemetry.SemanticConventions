namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A reference to an attribute carried by a signal, preserving the upstream requirement
/// level and whether the attribute itself is deprecated.
/// </summary>
public sealed class AttributeRef
{
    /// <summary>Initializes a new attribute reference.</summary>
    /// <param name="name">The attribute key (e.g. <c>disk.io.direction</c>).</param>
    /// <param name="type">The attribute value type (<c>string</c>, <c>int</c>, <c>enum</c>, <c>template</c>, ...).</param>
    /// <param name="requirementLevel">The requirement level of the attribute on the referencing signal.</param>
    /// <param name="isDeprecated">Whether the attribute itself is deprecated.</param>
    public AttributeRef(string name, string type, RequirementLevel requirementLevel, bool isDeprecated)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        RequirementLevel = requirementLevel;
        IsDeprecated = isDeprecated;
    }

    /// <summary>The attribute key (e.g. <c>disk.io.direction</c>).</summary>
    public string Name { get; }

    /// <summary>The attribute value type (<c>string</c>, <c>int</c>, <c>enum</c>, <c>template</c>, ...).</summary>
    public string Type { get; }

    /// <summary>The requirement level of the attribute on the referencing signal.</summary>
    public RequirementLevel RequirementLevel { get; }

    /// <summary>Whether the attribute itself is deprecated.</summary>
    public bool IsDeprecated { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
