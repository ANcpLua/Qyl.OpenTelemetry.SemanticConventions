using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.CodeFixes;

/// <summary>
///     Code fix provider for AL0124 - removes [AgentTraced] attribute from non-interceptable methods.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Qyl0405AgentTracedCodeFixProvider))]
[Shared]
public sealed class Qyl0405AgentTracedCodeFixProvider : CodeFixProvider {
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [Qyl0405NonInterceptableAgentTracedAnalyzer.DiagnosticId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not
            { } root) {
            return;
        }

        foreach (var diagnostic in context.Diagnostics) {
            var token = root.FindToken(diagnostic.Location.SourceSpan.Start);
            if (token.Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } methodDeclaration
                || SemconvCodeFixHelpers.FindAttributeByName(methodDeclaration, "AgentTraced") is not { } targetAttribute) {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    CodeFixResources.QYL0405CodeFixTitle,
                    _ => Task.FromResult(SemconvCodeFixHelpers.RemoveAttribute(context.Document, root, targetAttribute)),
                    nameof(CodeFixResources.QYL0405CodeFixTitle)),
                diagnostic);
        }
    }
}
