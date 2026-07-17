using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0108: Removes redundant [NoTrace] attribute from methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0107RedundantNoTraceCodeFixProvider))]
[Shared]
public sealed class Qyl0107RedundantNoTraceCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0107RedundantNoTraceAnalyzer.DiagnosticId];

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

        if (SemconvCodeFixHelpers.FindAttributeByName(method, "NoTrace") is not { } attributeSyntax) {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.QYL0107CodeFixTitle,
                _ => Task.FromResult(SemconvCodeFixHelpers.RemoveAttribute(context.Document, root, attributeSyntax)),
                nameof(Qyl0107RedundantNoTraceCodeFixProvider)),
            diagnostic);
    }
}
