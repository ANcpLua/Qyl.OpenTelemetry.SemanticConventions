namespace Qyl.Telemetry.SemanticConventions;

/// <summary>
/// A reference to an attribute carried by a signal, preserving the upstream requirement
/// level and its condition, whether the attribute itself is deprecated, when its value becomes
/// known, and whether the qyl collector persists it.
/// </summary>
public sealed class AttributeRef
{
    /// <summary>Initializes a new attribute reference.</summary>
    /// <param name="name">The attribute key (e.g. <c>disk.io.direction</c>).</param>
    /// <param name="type">The attribute value type (<c>string</c>, <c>int</c>, <c>enum</c>, <c>template</c>, ...).</param>
    /// <param name="requirementLevel">The requirement level of the attribute on the referencing signal.</param>
    /// <param name="isDeprecated">Whether the attribute itself is deprecated.</param>
    /// <param name="condition">The registry's condition text for the requirement level, or <see langword="null"/> when it states none.</param>
    /// <param name="phase">When the attribute's value becomes known.</param>
    /// <param name="isPersisted">Whether the qyl collector persists the attribute (<see cref="RegistryPolicy.IsPersisted"/>).</param>
    public AttributeRef(
        string name,
        string type,
        RequirementLevel requirementLevel,
        bool isDeprecated,
        string? condition,
        AttributePhase phase,
        bool isPersisted)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        RequirementLevel = requirementLevel;
        IsDeprecated = isDeprecated;
        Condition = condition;
        Phase = phase;
        IsPersisted = isPersisted;
    }

    /// <summary>The attribute key (e.g. <c>disk.io.direction</c>).</summary>
    public string Name { get; }

    /// <summary>The attribute value type (<c>string</c>, <c>int</c>, <c>enum</c>, <c>template</c>, ...).</summary>
    public string Type { get; }

    /// <summary>The requirement level of the attribute on the referencing signal.</summary>
    public RequirementLevel RequirementLevel { get; }

    /// <summary>Whether the attribute itself is deprecated.</summary>
    public bool IsDeprecated { get; }

    /// <summary>The registry's condition text for the requirement level, or <see langword="null"/> when it states none.</summary>
    public string? Condition { get; }

    /// <summary>When the attribute's value becomes known.</summary>
    public AttributePhase Phase { get; }

    /// <summary>Whether the qyl collector persists the attribute.</summary>
    public bool IsPersisted { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
