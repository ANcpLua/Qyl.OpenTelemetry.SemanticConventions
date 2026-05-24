// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SupplementalSemconvMigrationCodeFixProvider))]
public sealed class SupplementalSemconvMigrationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } =
    [
        "QYL0030",
        "QYL0031",
        "QYL0032",
    ];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            if (!IsExactReplacement(diagnostic)
                || !diagnostic.Properties.TryGetValue(
                    SupplementalSemconvMigrationAnalyzer.ReplacementNameProperty,
                    out var replacement)
                || string.IsNullOrWhiteSpace(replacement))
            {
                continue;
            }

            var literal = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<LiteralExpressionSyntax>();
            if (literal is null || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Replace with \"{replacement}\"",
                    ct => ReplaceLiteralAsync(document, literal, replacement!, ct),
                    nameof(SupplementalSemconvMigrationCodeFixProvider)),
                diagnostic);
        }
    }

    private static bool IsExactReplacement(Diagnostic diagnostic)
    {
        if (!diagnostic.Properties.TryGetValue(
                SupplementalSemconvMigrationAnalyzer.MigrationKindProperty,
                out var migrationKind))
        {
            return false;
        }

        return migrationKind == SemconvMigrationKind.ExactRename.ToString()
            || migrationKind == SemconvMigrationKind.ExactValueRename.ToString();
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
}
