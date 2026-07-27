// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>Shared diagnostic-location helper for invocation-based analyzers.</summary>
internal static class InvocationLocationExtensions {
    /// <summary>
    /// The location of the invoked method's name — tighter than the whole invocation,
    /// falling back to the full expression when the name cannot be isolated.
    /// </summary>
    public static Location GetMethodLocation(this InvocationExpressionSyntax invocation) =>
        invocation.Expression switch {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.GetLocation(),
            IdentifierNameSyntax identifier => identifier.GetLocation(),
            _ => invocation.GetLocation()
        };
}
