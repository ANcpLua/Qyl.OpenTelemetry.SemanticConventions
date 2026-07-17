// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Shared non-interceptable-method check: instrumentation attributes cannot intercept
/// abstract, extern, or partial-definition methods, so carrying one there is inert.
/// </summary>
internal static class NonInterceptableAttributeDetection {
    /// <summary>Reports <paramref name="rule"/> on non-interceptable methods carrying the attribute.</summary>
    public static void Register(AnalysisContext context, string attributeMetadataName, DiagnosticDescriptor rule) {
        context.RegisterCompilationStartAction(compilationContext => {
            if (compilationContext.Compilation.GetTypeByMetadataName(attributeMetadataName) is not { } attributeType) {
                return;
            }

            compilationContext.RegisterSymbolAction(
                ctx => AnalyzeMethod(ctx, attributeType, rule),
                SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol attributeType, DiagnosticDescriptor rule) {
        if (context.Symbol is not IMethodSymbol { IsAbstract: true } and not IMethodSymbol { IsExtern: true } and not IMethodSymbol { IsPartialDefinition: true }) {
            return;
        }

        var method = (IMethodSymbol)context.Symbol;

        if (method.HasAttribute(attributeType)) {
            context.ReportDiagnostic(Diagnostic.Create(
                rule,
                method.Locations.FirstOrDefault() ?? Location.None,
                method.Name));
        }
    }
}
