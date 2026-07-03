using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0109: Removes [Traced] attribute from non-interceptable methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0108NonInterceptableTracedCodeFixProvider))]
[Shared]
public sealed class Qyl0108NonInterceptableTracedCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0108NonInterceptableTracedAnalyzer.DiagnosticId];

    /// <summary>Gets the FixAll provider for batch fixing.</summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>Registers code fixes for the given context.</summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root) {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault() is not { } method) {
            return;
        }

        if (FindAttributeByName(method, "Traced") is not { } attributeSyntax) {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.QYL0108CodeFixTitle,
                _ => RemoveAttributeAsync(context.Document, root, attributeSyntax),
                nameof(Qyl0108NonInterceptableTracedCodeFixProvider)),
            diagnostic);
    }

    private static AttributeSyntax? FindAttributeByName(MemberDeclarationSyntax member, string attributeShortName) {
        foreach (var attributeList in member.AttributeLists) {
            foreach (var attribute in attributeList.Attributes) {
                var name = attribute.Name.ToString();
                if (name == attributeShortName || name.EndsWithOrdinal("." + attributeShortName) ||
                    name == attributeShortName + "Attribute" ||
                    name.EndsWithOrdinal("." + attributeShortName + "Attribute")) {
                    return attribute;
                }
            }
        }

        return null;
    }

    private static Task<Document> RemoveAttributeAsync(Document document, SyntaxNode root, AttributeSyntax attribute) {
        var newRoot = RemoveAttribute(root, attribute);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static SyntaxNode RemoveAttribute(SyntaxNode root, AttributeSyntax attribute) {
        if (attribute.Parent is not AttributeListSyntax attributeList) {
            return root;
        }

        if (attributeList.Attributes.Count == 1) {
            return root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia)!;
        }

        var newList = attributeList.WithAttributes(attributeList.Attributes.Remove(attribute));
        return root.ReplaceNode(attributeList, newList);
    }
}
