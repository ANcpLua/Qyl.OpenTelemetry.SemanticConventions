// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

internal static class SemconvIntentClassifier
{
    private const string SchemaUrlPrefix = "https://opentelemetry.io/schemas/";

    private static readonly Version CurrentSchemaVersion = new(1, 41, 0);

    private static readonly string[] DowngradeFragments =
    [
        "Legacy",
        "Deprecated",
        "Migration",
        "Compatibility",
        "Compat",
        "Fixture",
        "Snapshot",
        "Golden",
        "Schema",
        "Translation",
        "Translator",
        "Catalog",
        "Generated",
    ];

    public static SemconvLegacyMode GetLegacyMode(AnalyzerOptions options)
    {
        if (TryGetGlobalOption(options, "build_property.OtelSemConvLegacyMode", out var value))
        {
            if (string.Equals(value, "compatibility", StringComparison.OrdinalIgnoreCase))
            {
                return SemconvLegacyMode.Compatibility;
            }

            if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
            {
                return SemconvLegacyMode.Off;
            }
        }

        return SemconvLegacyMode.Production;
    }

    public static bool IsCompatibilityOrTestContext(OperationAnalysisContext context)
    {
        if (IsTestProject(context.Options))
        {
            return true;
        }

        var assemblyName = context.Compilation.AssemblyName;
        if (assemblyName is not null
            && assemblyName.EndsWith(".Tests", StringComparison.Ordinal))
        {
            return true;
        }

        if (IsDowngradedPath(context.Operation.Syntax.SyntaxTree.FilePath))
        {
            return true;
        }

        return IsDowngradedSymbol(context.ContainingSymbol)
            || HasTestAttribute(context.ContainingSymbol);
    }

    public static bool IsCatalogSource(IOperation operation)
    {
        var path = NormalizePath(operation.Syntax.SyntaxTree.FilePath);
        return path.EndsWith("/OpenTelemetryDeprecatedSemconvCatalog.cs", StringComparison.Ordinal)
            || path.EndsWith("/SemconvMigrationCatalogEntry.cs", StringComparison.Ordinal);
    }

    public static bool IsGeneratedSource(IOperation operation)
    {
        var path = NormalizePath(operation.Syntax.SyntaxTree.FilePath);
        return path.IndexOf("/Generated/", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf(".Generated.", StringComparison.OrdinalIgnoreCase) >= 0
            || path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasLegacySchemaUrlContext(IOperation operation)
    {
        var scope = FindContainingType(operation.Syntax);
        if (scope is null)
        {
            return false;
        }

        foreach (var descendant in scope.DescendantNodes())
        {
            if (descendant is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && IsLegacySchemaUrl(literal.Token.ValueText))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTestProject(AnalyzerOptions options) =>
        TryGetGlobalOption(options, "build_property.IsTestProject", out var value)
        && (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase));

    private static bool TryGetGlobalOption(
        AnalyzerOptions options,
        string key,
        [NotNullWhen(true)] out string? value)
    {
        var global = options.AnalyzerConfigOptionsProvider.GlobalOptions;
        return global.TryGetValue(key, out value)
            && !string.IsNullOrWhiteSpace(value);
    }

    private static bool IsDowngradedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalized = NormalizePath(path!);
        if (normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.IndexOf("/tests/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return ContainsDowngradeFragment(normalized);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/');

    private static bool IsDowngradedSymbol(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            if (ContainsDowngradeFragment(current.Name))
            {
                return true;
            }

            if (current is INamespaceSymbol)
            {
                break;
            }
        }

        return false;
    }

    private static bool HasTestAttribute(ISymbol? symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            foreach (var attribute in current.GetAttributes())
            {
                var name = attribute.AttributeClass?.Name;
                if (name is null)
                {
                    continue;
                }

                if (name is "FactAttribute"
                    or "TheoryAttribute"
                    or "TestAttribute"
                    or "TestCaseAttribute"
                    or "TestMethodAttribute"
                    or "TestClassAttribute")
                {
                    return true;
                }
            }

            if (current is INamespaceSymbol)
            {
                break;
            }
        }

        return false;
    }

    private static bool ContainsDowngradeFragment(string value)
    {
        foreach (var fragment in DowngradeFragments)
        {
            if (value.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static TypeDeclarationSyntax? FindContainingType(SyntaxNode syntax)
    {
        for (var current = syntax; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax typeDeclaration)
            {
                return typeDeclaration;
            }
        }

        return null;
    }

    private static bool IsLegacySchemaUrl(string value)
    {
        if (!value.StartsWith(SchemaUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var versionStart = SchemaUrlPrefix.Length;
        var versionEnd = versionStart;
        while (versionEnd < value.Length
            && (char.IsDigit(value[versionEnd]) || value[versionEnd] == '.'))
        {
            versionEnd++;
        }

        if (versionEnd <= versionStart)
        {
            return false;
        }

        var versionText = value.Substring(versionStart, versionEnd - versionStart).TrimEnd('.');
        return Version.TryParse(versionText, out var version)
            && version < CurrentSchemaVersion;
    }
}
