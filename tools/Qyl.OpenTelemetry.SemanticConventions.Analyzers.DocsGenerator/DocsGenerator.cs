// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   Top-level orchestrator. Owns the mode dispatch (<see cref="CliModes.Parse"/>),
///   the four generated-artifact pipelines (<c>Generate</c> + <c>Check</c>), and the
///   two source-side rewriters (<c>EnforceIds</c>, <c>RewriteShipped</c>). Every other
///   class in this project is pure logic invoked from here.
///
///   Extension point: each generated artifact (index, per-rule pages, migration
///   catalog, SARIF, editorconfig, Shipped.md Notes) is one numbered step in
///   <see cref="Generate"/> + <see cref="Check"/>. Adding a new artifact means adding
///   a focused renderer class and one numbered step in both methods.
/// </summary>
internal static class DocsGenerator
{
    public static int Run(string[] args)
    {
        var repoRoot = RepoLayout.FindRepoRoot(AppContext.BaseDirectory);
        var outputPath = RepoLayout.IndexPath(repoRoot);
        var mode = CliModes.Parse(args);

        // EnforceIds modes don't need the catalog (they walk source files), and we
        // want them to run on a clean assembly load even if the catalog has issues.
        if (mode is Mode.EnforceIdsCheck or Mode.EnforceIdsApply)
            return EnforceIdsRewriter.Run(repoRoot, apply: mode == Mode.EnforceIdsApply);

        // RewriteShipped also bypasses the catalog — it just walks DiagnosticAnalyzer
        // subclasses via reflection to map Id → ClassName for the
        // AnalyzerReleases.Shipped.md Notes column.
        if (mode == Mode.RewriteShipped)
            return ShippedNotesRewriter.Run(repoRoot);

        var stats = CatalogStatistics.Compute();
        return mode switch
        {
            Mode.Audit => Audit(stats),
            Mode.Check => Check(stats, outputPath, repoRoot),
            _ => Generate(stats, outputPath, repoRoot),
        };
    }

    private static int Audit(CatalogStatistics stats)
    {
        Console.Write(stats.RenderAudit());
        return 0;
    }

    /// <summary>
    ///   Drift detection: every generated artifact must already exist on disk and match
    ///   what the generator would emit right now. Returns 1 on the first mismatch so CI
    ///   surfaces the staleness early without printing the full diff.
    /// </summary>
    private static int Check(CatalogStatistics stats, string outputPath, string repoRoot)
    {
        var descriptors = DescriptorCatalog.GetDescriptors();
        var fixableIds = DescriptorCatalog.GetFixableDiagnosticIds();
        var idToClass = DescriptorCatalog.BuildIdToClassMap();

        // (1) Slim index file.
        if (!File.Exists(outputPath))
        {
            Console.Error.WriteLine($"Missing generated docs: {outputPath}");
            return 1;
        }
        if (!string.Equals(File.ReadAllText(outputPath), IndexDocsRenderer.Render(descriptors, fixableIds, idToClass), StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Index docs are stale: {Path.GetRelativePath(repoRoot, outputPath)}");
            return 1;
        }

        // (2) Per-rule pages under docs/rules/.
        var rulesDir = RepoLayout.RulesDir(repoRoot);
        var expectedRuleFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in descriptors)
        {
            if (!idToClass.TryGetValue(d.Id, out var className))
            {
                Console.Error.WriteLine($"Descriptor {d.Id} has no owning DiagnosticAnalyzer class — cannot place per-rule page.");
                return 1;
            }
            var symbolic = SymbolicNaming.ToSymbolicName(className);
            var rulePath = RepoLayout.RulePath(repoRoot, d.Id, symbolic);
            expectedRuleFiles.Add(Path.GetFileName(rulePath));

            if (!File.Exists(rulePath))
            {
                Console.Error.WriteLine($"Missing per-rule page: {Path.GetRelativePath(repoRoot, rulePath)}");
                return 1;
            }
            if (!string.Equals(File.ReadAllText(rulePath), RulePageRenderer.Render(d, className, fixableIds), StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Per-rule page is stale: {Path.GetRelativePath(repoRoot, rulePath)}");
                return 1;
            }

            // HelpLinkUri drift: descriptor's URL must equal what the generator would emit now.
            var expectedUri = RuleDocs.HelpLink(d.Id, symbolic);
            if (!string.Equals(d.HelpLinkUri, expectedUri, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"HelpLinkUri drift on {d.Id}: descriptor='{d.HelpLinkUri}' expected='{expectedUri}'");
                return 1;
            }
        }

        // Fail on stale files left over in docs/rules/ that no descriptor produces.
        if (Directory.Exists(rulesDir))
        {
            foreach (var file in Directory.EnumerateFiles(rulesDir, "*.md"))
            {
                if (!expectedRuleFiles.Contains(Path.GetFileName(file)))
                {
                    Console.Error.WriteLine($"Stale per-rule page (no matching descriptor): {Path.GetRelativePath(repoRoot, file)}");
                    return 1;
                }
            }
        }

        // (3) Migration catalog (qyl-specific tables that don't fit per-rule pages).
        var catalogPath = RepoLayout.MigrationCatalogPath(repoRoot);
        if (!File.Exists(catalogPath))
        {
            Console.Error.WriteLine($"Missing migration catalog: {Path.GetRelativePath(repoRoot, catalogPath)}");
            return 1;
        }
        if (!string.Equals(File.ReadAllText(catalogPath), MigrationCatalogRenderer.Render(stats), StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Migration catalog is stale: {Path.GetRelativePath(repoRoot, catalogPath)}");
            return 1;
        }

        // (4) SARIF v2.1.0 rule manifest for tool interop (Sonar bridges, GitHub
        // Advanced Security uploads, IDE rule catalogs).
        var sarifPath = RepoLayout.SarifPath(repoRoot);
        if (!File.Exists(sarifPath))
        {
            Console.Error.WriteLine($"Missing SARIF manifest: {Path.GetRelativePath(repoRoot, sarifPath)}");
            return 1;
        }
        if (!string.Equals(File.ReadAllText(sarifPath), SarifRenderer.Render(descriptors, idToClass), StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"SARIF manifest is stale: {Path.GetRelativePath(repoRoot, sarifPath)}");
            return 1;
        }

        // (5) Editorconfig profiles ship in the NuGet as ready-made severity profiles
        // consumers can drop into their repo.
        foreach (var (path, expected) in EditorconfigRenderer.EnumerateProfiles(repoRoot, descriptors))
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Missing editorconfig profile: {Path.GetRelativePath(repoRoot, path)}");
                return 1;
            }
            if (!string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Editorconfig profile is stale: {Path.GetRelativePath(repoRoot, path)}");
                return 1;
            }
        }

        // (6) AnalyzerReleases.Shipped.md Notes column carries the
        // "ClassName, [Documentation](url)" Microsoft-pattern attribution. RS2008
        // already enforces rows-per-descriptor; this check enforces the Notes shape
        // so the file can't drift via hand-edit.
        var shippedPath = RepoLayout.ShippedReleasesPath(repoRoot);
        if (File.Exists(shippedPath))
        {
            var existingShipped = File.ReadAllText(shippedPath);
            var expectedShipped = ShippedNotesRewriter.Rewrite(existingShipped);
            if (!string.Equals(existingShipped, expectedShipped, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Shipped.md Notes column is stale (run --rewrite-shipped): {Path.GetRelativePath(repoRoot, shippedPath)}");
                return 1;
            }
        }

        Console.WriteLine($"Index docs are up to date: {Path.GetRelativePath(repoRoot, outputPath)}");
        Console.WriteLine($"Per-rule pages are up to date ({descriptors.Count}).");
        Console.WriteLine($"Migration catalog is up to date: {Path.GetRelativePath(repoRoot, catalogPath)}");
        Console.WriteLine($"SARIF manifest is up to date: {Path.GetRelativePath(repoRoot, sarifPath)}");
        Console.WriteLine("Editorconfig profiles are up to date.");
        Console.WriteLine("Shipped.md Notes column is up to date.");
        Console.WriteLine("HelpLinkUri values match per-rule page URLs.");
        return 0;
    }

    /// <summary>
    ///   Writes every generated artifact. The order matches the numbering in
    ///   <see cref="Check"/> so the two methods stay in lockstep when a new artifact
    ///   is added.
    /// </summary>
    private static int Generate(CatalogStatistics stats, string outputPath, string repoRoot)
    {
        var descriptors = DescriptorCatalog.GetDescriptors();
        var fixableIds = DescriptorCatalog.GetFixableDiagnosticIds();
        var idToClass = DescriptorCatalog.BuildIdToClassMap();

        // (1) Slim index.
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, IndexDocsRenderer.Render(descriptors, fixableIds, idToClass));
        Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, outputPath)}");

        // (2) Per-rule pages.
        var rulesDir = RepoLayout.RulesDir(repoRoot);
        Directory.CreateDirectory(rulesDir);
        var expectedRuleFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in descriptors)
        {
            if (!idToClass.TryGetValue(d.Id, out var className)) continue;
            var symbolic = SymbolicNaming.ToSymbolicName(className);
            var rulePath = RepoLayout.RulePath(repoRoot, d.Id, symbolic);
            expectedRuleFiles.Add(Path.GetFileName(rulePath));
            File.WriteAllText(rulePath, RulePageRenderer.Render(d, className, fixableIds));
        }
        // Clean up stale rule pages from prior renames (otherwise --check fails afterward).
        foreach (var file in Directory.EnumerateFiles(rulesDir, "*.md"))
        {
            if (!expectedRuleFiles.Contains(Path.GetFileName(file)))
            {
                File.Delete(file);
                Console.WriteLine($"Removed stale {Path.GetRelativePath(repoRoot, file)}");
            }
        }
        Console.WriteLine($"Wrote {descriptors.Count} per-rule pages under docs/rules/");

        // (3) Migration catalog.
        var catalogPath = RepoLayout.MigrationCatalogPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllText(catalogPath, MigrationCatalogRenderer.Render(stats));
        Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, catalogPath)}");

        // (4) SARIF v2.1.0 rule manifest.
        var sarifPath = RepoLayout.SarifPath(repoRoot);
        File.WriteAllText(sarifPath, SarifRenderer.Render(descriptors, idToClass));
        Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, sarifPath)}");

        // (5) Editorconfig profiles.
        foreach (var (path, content) in EditorconfigRenderer.EnumerateProfiles(repoRoot, descriptors))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, path)}");
        }
        return 0;
    }
}
