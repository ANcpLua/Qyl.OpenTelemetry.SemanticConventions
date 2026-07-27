using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0107: Removes orphaned [TracedTag] attribute from parameters.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0106OrphanedTracedTagCodeFixProvider))]
[Shared]
public sealed class Qyl0106OrphanedTracedTagCodeFixProvider : CodeFixProvider {
    /// <summary>Gets the diagnostic IDs this provider can fix.</summary>
    public override ImmutableArray<string> FixableDiagnosticIds => [Qyl0106OrphanedTracedTagAnalyzer.DiagnosticId];

    /// <summary>Gets the FixAll provider for batch fixing.</summary>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <summary>Registers code fixes for the given context.</summary>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root) {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not ParameterSyntax parameter) {
            return;
        }

        if (SemconvCodeFixHelpers.FindAttributeByName(parameter, "TracedTag") is not { } attributeSyntax) {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                CodeFixResources.QYL0106CodeFixTitle,
                _ => Task.FromResult(SemconvCodeFixHelpers.RemoveAttribute(context.Document, root, attributeSyntax)),
                nameof(Qyl0106OrphanedTracedTagCodeFixProvider)),
            diagnostic);
    }
}
