// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>Shared namespace-shape detection for SemConv attribute classes.</summary>
internal static class SemconvNamespace
{
    private const string Root = "OpenTelemetry.SemanticConventions";

    /// <summary>
    /// Returns true if the type's suffix matches a recognised SemConv tier AND it lives
    /// in or under any namespace segment named <c>OpenTelemetry.SemanticConventions</c>
    /// (handles both upstream's flat layout and consumer-side nested layouts, e.g. qyl).
    /// When <paramref name="allowNonAttributesTiers"/> is <c>true</c>, the suffix check
    /// additionally accepts the three non-Attributes tiers Weaver SourceGeneration
    /// emits — <c>*Metrics</c>, <c>*Meters</c>, <c>*Activities</c> — gated behind
    /// <c>build_property.OtelSemConvNonAttributesTiers</c> so consumers explicitly
    /// opt into wider coverage.
    /// </summary>
    public static bool IsAttributesType(INamedTypeSymbol type, bool allowNonAttributesTiers = false)
    {
        if (!HasRecognisedTierSuffix(type.Name, allowNonAttributesTiers))
        {
            return false;
        }

        return IsInSemconvNamespace(type.ContainingNamespace);
    }

    private static bool HasRecognisedTierSuffix(string typeName, bool allowNonAttributesTiers)
    {
        if (typeName.EndsWith("Attributes", StringComparison.Ordinal))
        {
            return true;
        }

        if (!allowNonAttributesTiers)
        {
            return false;
        }

        return typeName.EndsWith("Metrics", StringComparison.Ordinal)
            || typeName.EndsWith("Meters", StringComparison.Ordinal)
            || typeName.EndsWith("Activities", StringComparison.Ordinal);
    }

    public static bool IsInSemconvNamespace(INamespaceSymbol? ns)
    {
        var s = ns?.ToDisplayString();
        if (s is null)
        {
            return false;
        }

        return s == Root
               || s.StartsWith(Root + ".", StringComparison.Ordinal)
               || s.Contains("." + Root + ".")
               || s.EndsWith("." + Root, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns true if the namespace's display string contains an <c>.Incubating</c>
    /// segment AND descends from a SemanticConventions root. Mirrors the AL0136
    /// detection: any namespace ending in or containing <c>.Incubating</c> under
    /// <c>OpenTelemetry.SemanticConventions</c> or <c>OpenTelemetry.SemConv</c>.
    /// </summary>
    public static bool IsIncubatingNamespace(INamespaceSymbol? ns)
    {
        var s = ns?.ToDisplayString();
        if (s is null || !s.Contains(".Incubating"))
        {
            return false;
        }

        return s.Contains("SemanticConventions") || s.Contains("SemConv");
    }
}
