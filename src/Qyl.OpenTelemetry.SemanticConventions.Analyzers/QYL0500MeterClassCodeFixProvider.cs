using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0071: Adds 'partial static' modifiers to [Meter] class.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0500MeterClassCodeFixProvider))]
[Shared]
public sealed class Qyl0500MeterClassCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0500MeterClassMustBePartialStaticAnalyzer.DiagnosticId];

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
                .AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault() is not { } classDeclaration) {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.QYL0500CodeFixTitle,
                c => MakePartialStaticAsync(context.Document, classDeclaration, root, c),
                nameof(Qyl0500MeterClassCodeFixProvider)),
            diagnostic);
    }

    private static Task<Document> MakePartialStaticAsync(
        Document document,
        ClassDeclarationSyntax classDeclaration,
        SyntaxNode root,
        CancellationToken _) {
        var modifiers = classDeclaration.Modifiers;

        var hasPartial = modifiers.Any(SyntaxKind.PartialKeyword);
        var hasStatic = modifiers.Any(SyntaxKind.StaticKeyword);

        var newModifiers = modifiers;

        if (!hasStatic) {
            var staticToken = SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space);

            var insertIndex = GetStaticInsertIndex(modifiers);
            newModifiers = newModifiers.Insert(insertIndex, staticToken);
        }

        if (!hasPartial) {
            var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space);
            newModifiers = newModifiers.Add(partialToken);
        }

        var newClassDeclaration = classDeclaration.WithModifiers(newModifiers);
        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);

        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static int GetStaticInsertIndex(SyntaxTokenList modifiers) {
        for (var i = 0; i < modifiers.Count; i++) {
            var kind = modifiers[i].Kind();
            if (kind is not (SyntaxKind.PublicKeyword or SyntaxKind.PrivateKeyword or
                SyntaxKind.ProtectedKeyword or SyntaxKind.InternalKeyword)) {
                return i;
            }
        }

        return modifiers.Count;
    }
}
