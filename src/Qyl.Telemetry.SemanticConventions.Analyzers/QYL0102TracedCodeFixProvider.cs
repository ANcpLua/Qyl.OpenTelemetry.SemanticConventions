using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0073: Adds a default ActivitySourceName to [Traced] attribute.
/// </summary>
/// <remarks>
///     <para>
///         This code fix provides a default ActivitySourceName based on the containing type's
///         fully qualified name. For example, a class named <c>MyApp.Services.OrderService</c>
///         would get the ActivitySourceName <c>"MyApp.Services.OrderService"</c>.
///     </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0102TracedCodeFixProvider))]
[Shared]
public sealed class Qyl0102TracedCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0102TracedActivitySourceNameAnalyzer.DiagnosticId];

    /// <summary>Gets the FixAll provider for batch fixing.</summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>Registers code fixes for the given context.</summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not
            { } root) {
            return;
        }

        var diagnostic = context.Diagnostics[0];

        if (root.FindNode(diagnostic.Location.SourceSpan)
                .AncestorsAndSelf().OfType<AttributeSyntax>().FirstOrDefault() is not { } attributeSyntax) {
            return;
        }

        if (await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false) is not
            { } semanticModel) {
            return;
        }

        var suggestedName = GetSuggestedActivitySourceName(attributeSyntax, semanticModel, context.CancellationToken);

        context.RegisterCodeFix(
            CodeAction.Create(
                string.Format(System.Globalization.CultureInfo.InvariantCulture, CodeFixResources.QYL0102CodeFixTitle, suggestedName),
                c => AddActivitySourceNameAsync(context.Document, attributeSyntax, suggestedName, root, c),
                nameof(Qyl0102TracedCodeFixProvider)),
            diagnostic);
    }

    private static string GetSuggestedActivitySourceName(
        SyntaxNode attribute,
        SemanticModel semanticModel,
        CancellationToken cancellationToken) {
        if (attribute.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is not { } containingType) {
            return "MyApp";
        }

        if (semanticModel.GetDeclaredSymbol(containingType, cancellationToken) is not { } typeSymbol) {
            return containingType.Identifier.Text;
        }

        var fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return fullName.ReplaceOrdinal("global::", "") ?? fullName;
    }

    private static Task<Document> AddActivitySourceNameAsync(
        Document document,
        AttributeSyntax attribute,
        string sourceName,
        SyntaxNode root,
        CancellationToken _) {
        AttributeSyntax newAttribute;

        if (attribute.ArgumentList is null || attribute.ArgumentList.Arguments.Count is 0) {
            var argument = SyntaxFactory.AttributeArgument(
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.StringLiteralExpression,
                    SyntaxFactory.Literal(sourceName)));

            var argumentList = SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(argument));

            newAttribute = attribute.WithArgumentList(argumentList);
        } else {
            var firstArg = attribute.ArgumentList.Arguments[0];
            if (firstArg.Expression is LiteralExpressionSyntax { Token.ValueText: "" or " " or "  " }) {
                var newArg = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(sourceName)));

                var newArguments = attribute.ArgumentList.Arguments.Replace(firstArg, newArg);
                var newArgumentList = attribute.ArgumentList.WithArguments(newArguments);
                newAttribute = attribute.WithArgumentList(newArgumentList);
            } else {
                var namedArg = SyntaxFactory.AttributeArgument(
                    SyntaxFactory.NameEquals("ActivitySourceName"),
                    null,
                    SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(sourceName)));

                var newArguments = attribute.ArgumentList.Arguments.Insert(0, namedArg);
                var newArgumentList = attribute.ArgumentList.WithArguments(newArguments);
                newAttribute = attribute.WithArgumentList(newArgumentList);
            }
        }

        var newRoot = root.ReplaceNode(attribute, newAttribute);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
