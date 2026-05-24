// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Suppresses a curated set of specific legacy QYL diagnostic IDs when they fire
/// inside a class, struct, record, or member whose name is one of the well-known
/// legacy/compatibility shapes that intentionally model an older schema:
///
/// <list type="bullet">
///   <item><c>Legacy*</c> / <c>*Legacy</c> (e.g. <c>LegacyTelemetryShim</c>)</item>
///   <item><c>*CompatShim</c> / <c>*Compatibility*</c></item>
///   <item><c>*MigrationFixture</c> / <c>*MigrationMap</c></item>
///   <item><c>*SchemaTranslator</c></item>
///   <item><c>*ObsoleteSemconv*</c> / <c>*DeprecatedSemconv*</c></item>
/// </list>
///
/// This is the structured alternative to scattering <c>#pragma warning disable</c>
/// across compatibility code. Pair it with the
/// <c>build_property.OtelSemConvLegacyMode = compatibility</c> escape hatch when
/// the whole project is a translator/shim; use this per-type suppressor when only
/// a handful of types intentionally emit older schemas inside an otherwise
/// production-grade project.
///
/// The suppressor is intentionally name-shape based (not attribute-based) so it
/// does not require consumers to depend on this package at compile time.
/// </summary>
// DiagnosticSuppressor shares the analyzer host loading mechanism, so the
// Roslyn analyzer pack's RS1001 rule still expects the [DiagnosticAnalyzer]
// marker even though SuppressionDescriptors are independent of language-bound
// diagnostic registration. Marking the suppressor as CSharp matches the
// language scope of the QYL* analyzers it suppresses.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SemconvLegacyContextSuppressor : DiagnosticSuppressor
{
    private const string Justification =
        "Diagnostic suppressed: the containing type name matches a well-known legacy/"
        + "compatibility/migration shape (Legacy*, *CompatShim, *MigrationFixture, "
        + "*SchemaTranslator, *DeprecatedSemconv*). Use "
        + "build_property.OtelSemConvLegacyMode=compatibility for whole-project "
        + "downgrades; this suppressor handles per-type legacy code in otherwise "
        + "production projects.";

    private static readonly ImmutableArray<string> s_suppressedDiagnosticIds =
        ImmutableArray.Create(
            "QYL0001",
            "QYL0002",
            "QYL0005",
            "QYL0010",
            "QYL0011",
            "QYL0012",
            "QYL0014",
            "QYL0021",
            "QYL0030",
            "QYL0031",
            "QYL0032");

    private static readonly ImmutableDictionary<string, SuppressionDescriptor> s_suppressions =
        BuildSuppressions();

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        s_suppressions.Values.ToImmutableArray();

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (!s_suppressions.TryGetValue(diagnostic.Id, out var descriptor))
            {
                continue;
            }

            var location = diagnostic.Location;
            if (location.SourceTree is not { } tree)
            {
                continue;
            }

            var root = tree.GetRoot(context.CancellationToken);
            var node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
            if (node is null)
            {
                continue;
            }

            var model = context.GetSemanticModel(tree);
            if (!IsInLegacyShape(node, model, context.CancellationToken))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(descriptor, diagnostic));
        }
    }

    private static bool IsInLegacyShape(
        SyntaxNode node,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            var symbol = model.GetDeclaredSymbol(current, cancellationToken);
            if (symbol is null)
            {
                continue;
            }

            if (HasLegacyShape(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLegacyShape(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            var name = current.Name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (MatchesLegacyShape(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesLegacyShape(string name)
    {
        if (name.StartsWith("Legacy", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.EndsWith("Legacy", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.EndsWith("CompatShim", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.IndexOf("Compatibility", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (name.EndsWith("MigrationFixture", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.EndsWith("MigrationMap", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.EndsWith("SchemaTranslator", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.IndexOf("ObsoleteSemconv", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        if (name.IndexOf("DeprecatedSemconv", StringComparison.Ordinal) >= 0)
        {
            return true;
        }

        return false;
    }

    private static ImmutableDictionary<string, SuppressionDescriptor> BuildSuppressions()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, SuppressionDescriptor>(StringComparer.Ordinal);
        foreach (var id in s_suppressedDiagnosticIds)
        {
            builder.Add(
                id,
                new SuppressionDescriptor(
                    id: "QYL9" + id.Substring(4),
                    suppressedDiagnosticId: id,
                    justification: Justification));
        }

        return builder.ToImmutable();
    }
}
