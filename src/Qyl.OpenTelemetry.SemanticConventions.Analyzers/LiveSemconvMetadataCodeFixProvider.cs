// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LiveSemconvMetadataCodeFixProvider))]
public sealed class LiveSemconvMetadataCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    [
        "QYL0010",
        "QYL0012",
        "QYL0014",
    ];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (var diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(
                    SemconvCodeFixHelpers.ReplacementValueProperty,
                    out var replacement)
                || string.IsNullOrWhiteSpace(replacement))
            {
                continue;
            }

            if (diagnostic.Id == "QYL0010")
            {
                await RegisterTypedConstantFixAsync(context, diagnostic, replacement!).ConfigureAwait(false);
                continue;
            }

            await RegisterLiteralFixAsync(context, diagnostic, replacement!).ConfigureAwait(false);
        }
    }

    private static async Task RegisterTypedConstantFixAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        string replacement)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var name = node.FirstAncestorOrSelf<SimpleNameSyntax>();
        if (name is null)
        {
            return;
        }

        var symbol = semanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol as IFieldSymbol;
        if (symbol is null)
        {
            return;
        }

        var replacementField = FindReplacementField(semanticModel.Compilation, replacement);
        if (replacementField is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Replace with {replacementField.ContainingType.Name}.{replacementField.Name}",
                ct => ReplaceTypedConstantAsync(document, name, symbol, replacementField, ct),
                nameof(LiveSemconvMetadataCodeFixProvider) + ".TypedConstant"),
            diagnostic);
    }

    private static async Task<Document> ReplaceTypedConstantAsync(
        Document document,
        SimpleNameSyntax originalName,
        IFieldSymbol originalField,
        IFieldSymbol replacementField,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (originalName.Parent is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name == originalName
            && SymbolEqualityComparer.Default.Equals(originalField.ContainingType, replacementField.ContainingType))
        {
            var replacementName = SyntaxFactory.IdentifierName(replacementField.Name)
                .WithTriviaFrom(originalName);
            return document.WithSyntaxRoot(root.ReplaceNode(originalName, replacementName));
        }

        var qualified = CreateQualifiedFieldExpression(replacementField)
            .WithTriviaFrom(originalName.Parent is MemberAccessExpressionSyntax containingMemberAccess
                && containingMemberAccess.Name == originalName
                    ? containingMemberAccess
                    : originalName);

        var nodeToReplace = originalName.Parent is MemberAccessExpressionSyntax parentMemberAccess
            && parentMemberAccess.Name == originalName
                ? (SyntaxNode)parentMemberAccess
                : originalName;
        return document.WithSyntaxRoot(root.ReplaceNode(nodeToReplace, qualified));
    }

    private static async Task RegisterLiteralFixAsync(
        CodeFixContext context,
        Diagnostic diagnostic,
        string replacement)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<LiteralExpressionSyntax>();
        if (literal is null || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                $"Replace with \"{replacement}\"",
                ct => ReplaceLiteralAsync(document, literal, replacement, ct),
                nameof(LiveSemconvMetadataCodeFixProvider) + ".Literal"),
            diagnostic);
    }

    private static async Task<Document> ReplaceLiteralAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string replacement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacementLiteral = SemconvCodeFixHelpers.CreateReplacementLiteral(literal, replacement);
        return document.WithSyntaxRoot(root.ReplaceNode(literal, replacementLiteral));
    }

    private static IFieldSymbol? FindReplacementField(
        Compilation compilation,
        string replacement)
    {
        return FindReplacementField(compilation.GlobalNamespace, replacement);
    }

    private static IFieldSymbol? FindReplacementField(
        INamespaceSymbol ns,
        string replacement)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string value,
                    } field
                    && string.Equals(value, replacement, StringComparison.Ordinal))
                {
                    return field;
                }
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            var result = FindReplacementField(nested, replacement);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private static ExpressionSyntax CreateQualifiedFieldExpression(IFieldSymbol field)
    {
        var qualifiedName = field.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            + "."
            + field.Name;
        return SyntaxFactory.ParseExpression(qualifiedName);
    }
}
