// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>Shared [Traced]-attribute facts for instrumentation analyzers.</summary>
internal static class TracedAttributeFacts {
    /// <summary>
    /// True when the type or any of its base types carries the attribute. This is a
    /// receiver base-type walk — distinct from attribute-class inheritance, which
    /// upstream <c>HasAttribute(inherits:)</c> models.
    /// </summary>
    public static bool HasTracedOnTypeChain(INamedTypeSymbol? type, INamedTypeSymbol tracedType) {
        for (var current = type; current is not null; current = current.BaseType) {
            if (current.HasAttribute(tracedType)) {
                return true;
            }
        }

        return false;
    }
}
