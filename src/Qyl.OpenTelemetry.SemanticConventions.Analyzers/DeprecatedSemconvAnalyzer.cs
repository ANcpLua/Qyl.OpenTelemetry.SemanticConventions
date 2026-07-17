// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0003: Flags references to <c>[Obsolete]</c> <c>const string</c> fields under
/// <c>OpenTelemetry.SemanticConventions.Attributes.*</c>.
/// </summary>
/// <remarks>
/// The deprecation map is the consumer's own referenced
/// <c>OpenTelemetry.SemanticConventions</c> assembly — there is no hard-coded catalog
/// in this analyzer. Whatever Weaver emits as <c>[Obsolete("Replaced by ...")]</c>
/// is what we surface.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedSemconvAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.DeprecatedSemconvConstant];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var allowNonAttributesTiers = SemconvAnalyzerOptions.ShouldAllowNonAttributesTiers(
                start.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
            start.RegisterOperationAction(
                ctx => AnalyzeFieldReference(ctx, allowNonAttributesTiers),
                OperationKind.FieldReference);
        });
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext context, bool allowNonAttributesTiers)
    {
        var operation = (IFieldReferenceOperation)context.Operation;
        var field = operation.Field;

        if (field.IsConst is false || field.Type.SpecialType != SpecialType.System_String)
        {
            return;
        }

        var containingType = field.ContainingType;
        if (containingType is null || !SemconvNamespace.IsAttributesType(containingType, allowNonAttributesTiers))
        {
            return;
        }

        var obsolete = field.GetAttribute("System.ObsoleteAttribute");
        if (obsolete is null)
        {
            return;
        }

        var message = SemconvCodeFixHelpers.GetObsoleteMessage(obsolete);
        var displayName = $"{containingType.Name}.{field.Name}";
        var properties = ImmutableDictionary<string, string?>.Empty;
        if (SemconvCodeFixHelpers.TryExtractExactReplacement(message, out var replacement))
        {
            properties = properties.Add(SemconvCodeFixHelpers.ReplacementValueProperty, replacement);
        }

        // Squiggle just the field name, not the full qualified expression.
        var location = operation.Syntax switch
        {
            MemberAccessExpressionSyntax member => member.Name.GetLocation(),
            _ => operation.Syntax.GetLocation(),
        };

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DeprecatedSemconvConstant,
            location,
            properties,
            displayName,
            message));
    }
}
