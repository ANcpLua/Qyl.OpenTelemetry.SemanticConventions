// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0021: Flags references to members under any
/// <c>*.SemanticConventions.Incubating</c> namespace from within library projects
/// (non-exe, non-test). The recommended mitigation is to copy the constant locally;
/// the analyzer suppresses itself when the usage is inside a <c>const</c> field
/// declaration (the local-copy pattern).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IncubatingSemconvInLibraryAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] TestAssemblyAttributeNames =
    [
        "Xunit.TestFrameworkAttribute",
        "Xunit.Sdk.TestFrameworkAttribute",
        "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute",
    ];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.IncubatingSemconvInLibrary];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (!IsLibraryProject(context.Compilation))
        {
            return;
        }

        context.RegisterOperationAction(
            AnalyzeMemberReference,
            OperationKind.FieldReference,
            OperationKind.PropertyReference,
            OperationKind.MethodReference,
            OperationKind.Invocation);
    }

    private static void AnalyzeMemberReference(OperationAnalysisContext context)
    {
        var containingType = context.Operation switch
        {
            IFieldReferenceOperation field => field.Field.ContainingType,
            IPropertyReferenceOperation property => property.Property.ContainingType,
            IMethodReferenceOperation methodRef => methodRef.Method.ContainingType,
            IInvocationOperation invocation => invocation.TargetMethod.ContainingType,
            _ => null,
        };

        if (containingType is null
            || !SemconvNamespace.IsIncubatingNamespace(containingType.ContainingNamespace))
        {
            return;
        }

        // Suppress when the usage is itself a const-field declaration — the
        // local-copy mitigation pattern.
        if (IsInsideConstFieldDeclaration(context.Operation.Syntax))
        {
            return;
        }

        var memberName = context.Operation switch
        {
            IFieldReferenceOperation field => field.Field.Name,
            IPropertyReferenceOperation property => property.Property.Name,
            IMethodReferenceOperation methodRef => methodRef.Method.Name,
            IInvocationOperation invocation => invocation.TargetMethod.Name,
            _ => "?",
        };

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.IncubatingSemconvInLibrary,
            context.Operation.Syntax.GetLocation(),
            $"{containingType.ToDisplayString()}.{memberName}"));
    }

    private static bool IsLibraryProject(Compilation compilation)
    {
        if (compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication)
        {
            return false;
        }

        var name = compilation.AssemblyName;
        if (name is not null
            && (name.EndsWith(".Tests", StringComparison.Ordinal)
                || name.EndsWith(".Test", StringComparison.Ordinal)
                || name.Contains(".Tests.")))
        {
            return false;
        }

        foreach (var attributeTypeName in TestAssemblyAttributeNames)
        {
            if (compilation.GetTypeByMetadataName(attributeTypeName) is not null)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInsideConstFieldDeclaration(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is FieldDeclarationSyntax field
                && field.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)))
            {
                return true;
            }
        }
        return false;
    }
}
