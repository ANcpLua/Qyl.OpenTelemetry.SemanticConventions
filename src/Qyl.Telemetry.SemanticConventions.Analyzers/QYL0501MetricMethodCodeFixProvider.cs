using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0072: Adds 'partial' modifier to metric methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0501MetricMethodCodeFixProvider))]
[Shared]
public sealed class Qyl0501MetricMethodCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0501MetricMethodMustBePartialAnalyzer.DiagnosticId];

    /// <summary>Gets the FixAll provider for batch fixing.</summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>Registers code fixes for the given context.</summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not
            { } root) {
            return;
        }

        var diagnostic = context.Diagnostics[0];

        if (root.FindToken(diagnostic.Location.SourceSpan.Start).Parent?
                .AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } methodDeclaration) {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.QYL0501CodeFixTitle,
                c => MakePartialAsync(context.Document, methodDeclaration, root, c),
                nameof(Qyl0501MetricMethodCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> MakePartialAsync(
        Document document,
        MethodDeclarationSyntax methodDeclaration,
        SyntaxNode root,
        CancellationToken _) {
        var modifiers = methodDeclaration.Modifiers;

        if (modifiers.Any(SyntaxKind.PartialKeyword)) {
            return Task.FromResult(document);
        }

        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
        var newModifiers = modifiers.Add(partialToken);


        var newMethodDeclaration = methodDeclaration
            .WithModifiers(newModifiers)
            .WithBody(null)
            .WithExpressionBody(null)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

        var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
