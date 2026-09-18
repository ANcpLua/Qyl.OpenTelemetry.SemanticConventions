// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>Shared receiver-type check for OTel builder fluent-call analyzers.</summary>
internal static class BuilderCallDetection {
    /// <summary>
    /// Returns true when the invocation is a member access whose receiver type is, inherits
    /// from, or implements any of the given builder types. The equality case is the common
    /// one: the <c>builder</c> parameter of <c>WithTracing(builder => ...)</c> is typed as
    /// <c>TracerProviderBuilder</c> itself, not as a subclass.
    /// </summary>
    public static bool IsBuilderCall(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        ImmutableArray<INamedTypeSymbol> builderTypes,
        CancellationToken cancellationToken) {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || ModelExtensions.GetTypeInfo(semanticModel, memberAccess.Expression, cancellationToken).Type is not { } receiverType) {
            return false;
        }

        return builderTypes.Any(builderType =>
            receiverType.IsEqualTo(builderType)
            || receiverType.InheritsFrom(builderType)
            || receiverType.Implements(builderType));
    }
}
