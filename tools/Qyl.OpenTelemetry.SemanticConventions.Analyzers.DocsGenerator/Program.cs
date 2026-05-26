// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers;

return DocsGenerator.Run(args);

file static class DocsGenerator
{
    private const string PackageName = "Qyl.OpenTelemetry.SemanticConventions.Analyzers";
    private const string ProjectRelativePath = "tools/Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator";
    private const string SolutionFileName = "Qyl.OpenTelemetry.SemanticConventions.slnx";

    public static int Run(string[] args)
    {
        var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        var outputPath = Path.Combine(repoRoot, "docs", PackageName + ".md");
        var mode = ParseMode(args);

        // EnforceIds modes don't need the catalog (they walk source files), and we
        // want them to run on a clean assembly load even if the catalog has issues.
        if (mode is Mode.EnforceIdsCheck or Mode.EnforceIdsApply)
            return EnforceIds(repoRoot, apply: mode == Mode.EnforceIdsApply);

        // RewriteShipped also bypasses the catalog — it just walks DiagnosticAnalyzer
        // subclasses via reflection to map Id → ClassName for the AnalyzerReleases.Shipped.md
        // Notes column.
        if (mode == Mode.RewriteShipped)
            return RewriteShipped(repoRoot);

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

    private static int Check(CatalogStatistics stats, string outputPath, string repoRoot)
    {
        if (!File.Exists(outputPath))
        {
            Console.Error.WriteLine($"Missing generated docs: {outputPath}");
            return 1;
        }

        if (!string.Equals(File.ReadAllText(outputPath), RenderMarkdown(stats), StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Generated docs are stale: {outputPath}");
            return 1;
        }

        // Editorconfig profiles must also be up to date — these ship in the NuGet as
        // ready-made severity profiles consumers can drop into their repo.
        foreach (var (relPath, expected) in EnumerateEditorconfigProfiles(repoRoot))
        {
            if (!File.Exists(relPath))
            {
                Console.Error.WriteLine($"Missing editorconfig profile: {Path.GetRelativePath(repoRoot, relPath)}");
                return 1;
            }
            if (!string.Equals(File.ReadAllText(relPath), expected, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Editorconfig profile is stale: {Path.GetRelativePath(repoRoot, relPath)}");
                return 1;
            }
        }

        // AnalyzerReleases.Shipped.md Notes column carries the
        //   "ClassName, [Documentation](url)" Microsoft-pattern attribution. RS2008
        // already enforces that rows exist for every descriptor; this check
        // additionally enforces the Notes shape so the file can't drift via hand-edit.
        var shippedPath = ShippedReleasesPath(repoRoot);
        if (File.Exists(shippedPath))
        {
            var existingShipped = File.ReadAllText(shippedPath);
            var expectedShipped = RewriteShippedNotes(existingShipped);
            if (!string.Equals(existingShipped, expectedShipped, StringComparison.Ordinal))
            {
                Console.Error.WriteLine(
                    $"Shipped.md Notes column is stale (run --rewrite-shipped): {Path.GetRelativePath(repoRoot, shippedPath)}");
                return 1;
            }
        }

        Console.WriteLine($"Generated docs are up to date: {Path.GetRelativePath(repoRoot, outputPath)}");
        Console.WriteLine($"Editorconfig profiles are up to date.");
        Console.WriteLine($"Shipped.md Notes column is up to date.");
        return 0;
    }

    private static int Generate(CatalogStatistics stats, string outputPath, string repoRoot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, RenderMarkdown(stats));
        Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, outputPath)}");

        foreach (var (path, content) in EnumerateEditorconfigProfiles(repoRoot))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            Console.WriteLine($"Wrote {Path.GetRelativePath(repoRoot, path)}");
        }
        return 0;
    }

    /// <summary>
    ///   Emits the three canonical editorconfig severity profiles consumers can drop into
    ///   their repo to switch the whole QYL band in one line. Mirrors the MS NetAnalyzers
    ///   <c>rulesets/</c> + <c>editorconfig/</c> pattern at our scale.
    ///   <list type="bullet">
    ///     <item><c>Default.editorconfig</c> — each rule at its descriptor-declared default severity</item>
    ///     <item><c>AllRulesAsErrors.editorconfig</c> — every rule promoted to error</item>
    ///     <item><c>AllRulesDisabled.editorconfig</c> — every rule silenced</item>
    ///   </list>
    ///   Yields (absolute path, content) pairs so both <c>Generate</c> and <c>Check</c> share
    ///   the source-of-truth content without re-deriving it.
    /// </summary>
    private static IEnumerable<(string AbsolutePath, string Content)> EnumerateEditorconfigProfiles(string repoRoot)
    {
        var descriptors = GetDescriptors();
        var dir = Path.Combine(repoRoot, "docs", "editorconfig");

        yield return (Path.Combine(dir, "Default.editorconfig"),
            RenderEditorconfig(descriptors, "Each QYL rule at its descriptor-declared default severity.",
                d => SeverityToken(d.DefaultSeverity)));

        yield return (Path.Combine(dir, "AllRulesAsErrors.editorconfig"),
            RenderEditorconfig(descriptors, "Every QYL rule promoted to error. Use for strict CI.",
                _ => "error"));

        yield return (Path.Combine(dir, "AllRulesDisabled.editorconfig"),
            RenderEditorconfig(descriptors, "Every QYL rule silenced. Use to opt the whole band out.",
                _ => "none"));
    }

    private static string RenderEditorconfig(
        IReadOnlyList<DiagnosticDescriptor> descriptors,
        string headerComment,
        Func<DiagnosticDescriptor, string> severityFor)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# <auto-generated /> Edits made by hand will be overwritten by");
        sb.AppendLine($"# {ProjectRelativePath}. Re-run `./build.sh GenerateDocs` after");
        sb.AppendLine("# changing analyzer descriptors. See docs/" + PackageName + ".md for rule details.");
        sb.AppendLine($"# {headerComment}");
        sb.AppendLine();
        sb.AppendLine("root = false");
        sb.AppendLine();
        sb.AppendLine("[*.{cs,vb}]");
        foreach (var d in descriptors)
            sb.AppendLine($"dotnet_diagnostic.{d.Id}.severity = {severityFor(d)}");
        return sb.ToString().ReplaceLineEndings("\n");
    }

    private static string SeverityToken(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => "error",
        DiagnosticSeverity.Warning => "warning",
        DiagnosticSeverity.Info => "suggestion",
        DiagnosticSeverity.Hidden => "silent",
        _ => "default",
    };

    private static Mode ParseMode(string[] args)
    {
        // Nuke's DotNetRunSettings.SetApplicationArguments passes a single quoted
        // string, so "--enforce-ids --apply" arrives as args[0] instead of two
        // separate args. Flatten on whitespace so both invocation shapes work.
        var flat = args
            .SelectMany(a => a.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        var enforce = flat.Any(a => IsFlag(a, "enforce-ids"));
        var apply = flat.Any(a => IsFlag(a, "apply"));
        if (enforce) return apply ? Mode.EnforceIdsApply : Mode.EnforceIdsCheck;

        if (flat.Any(a => IsFlag(a, "rewrite-shipped"))) return Mode.RewriteShipped;

        foreach (var arg in flat)
        {
            if (IsFlag(arg, "audit")) return Mode.Audit;
            if (IsFlag(arg, "check") || Eq(arg, "validate")) return Mode.Check;
        }
        return Mode.Generate;

        static bool IsFlag(string arg, string name) => Eq(arg, name) || Eq(arg, "--" + name);
        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///   Walks every analyzer source file under <c>src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/</c>
    ///   and aligns class names, XML doc summaries, and <c>DiagnosticId</c>-const docs with the runtime
    ///   <c>DiagnosticDescriptor</c> each analyzer/code-fix-provider registers. The runtime descriptor
    ///   is the source of truth — RS2008 plus <c>AnalyzerReleases.Shipped.md</c> already lock that
    ///   surface, so this tool just propagates that authority into source.
    ///
    ///   Class renames are global within the analyzer project (covers cross-file references such as a
    ///   code-fix provider that reads <c>Foo.DiagnosticId</c>), driven by both <see cref="DiagnosticAnalyzer"/>
    ///   subclasses (via <c>SupportedDiagnostics</c>) and <see cref="CodeFixProvider"/> subclasses
    ///   (via <c>FixableDiagnosticIds</c>). Per-file fixes (XML doc summary, const doc, const value
    ///   sanity) are anchored on the relevant syntax node's trivia so cross-references like
    ///   <c>see QYL0009</c> elsewhere in a file are never rewritten.
    /// </summary>
    private static int EnforceIds(string repoRoot, bool apply)
    {
        var analyzersDir = Path.Combine(repoRoot,
            "src", "Qyl.OpenTelemetry.SemanticConventions.Analyzers");

        var (analyzerIds, codeFixIds) = BuildClassMaps();

        var perFileFixes = new Dictionary<string, List<(string Description, Func<string, string> Apply)>>(
            StringComparer.Ordinal);
        var classRenames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var path in Directory.EnumerateFiles(analyzersDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var src = File.ReadAllText(path);
            var tree = CSharpSyntaxTree.ParseText(src);
            var classNode = tree.GetCompilationUnitRoot()
                .DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classNode is null) continue;

            var className = classNode.Identifier.Text;

            string? realId = null;
            var isAnalyzer = false;
            if (analyzerIds.TryGetValue(className, out var id))
            {
                realId = id;
                isAnalyzer = true;
            }
            else if (codeFixIds.TryGetValue(className, out id))
            {
                realId = id;
            }
            else continue;

            // (1) Class rename when name carries an Al/Qyl/QYL numeric prefix.
            var prefixMatch = Regex.Match(className, @"^(?:Al|Qyl|QYL)(\d{4})(.+)$");
            if (prefixMatch.Success)
            {
                var expectedClassName = $"Qyl{realId[3..]}{prefixMatch.Groups[2].Value}";
                if (className != expectedClassName)
                    classRenames[className] = expectedClassName;
            }

            // (2) Class XML doc summary: rewrite "/// AL00XX:" / "/// QYL00XX:" tokens that disagree
            //     with realId. Anchored on the class's leading trivia.
            var classTrivia = classNode.GetLeadingTrivia().ToFullString();
            var fixedClassTrivia = Regex.Replace(
                classTrivia,
                @"(///\s*)(?:AL|QYL)\d{4}:",
                m => m.Groups[1].Value + realId + ":");
            if (fixedClassTrivia != classTrivia)
            {
                var oldT = classTrivia;
                var newT = fixedClassTrivia;
                AddFix(perFileFixes, path,
                    $"class XML doc summary -> {realId}:",
                    s => s.Replace(oldT, newT));
            }

            // (3) DiagnosticId const docs + value — only on analyzer classes.
            if (isAnalyzer)
            {
                var diagIdField = classNode.Members.OfType<FieldDeclarationSyntax>()
                    .FirstOrDefault(f => f.Declaration.Variables
                        .Any(v => v.Identifier.Text == "DiagnosticId"));
                if (diagIdField is not null)
                {
                    var fieldTrivia = diagIdField.GetLeadingTrivia().ToFullString();
                    var fixedFieldTrivia = Regex.Replace(
                        fieldTrivia,
                        @"\bfor (?:AL|QYL)\d{4}\b",
                        $"for {realId}");
                    if (fixedFieldTrivia != fieldTrivia)
                    {
                        var oldT = fieldTrivia;
                        var newT = fixedFieldTrivia;
                        AddFix(perFileFixes, path,
                            $"DiagnosticId const doc -> for {realId}",
                            s => s.Replace(oldT, newT));
                    }

                    if (diagIdField.Declaration.Variables.First().Initializer?.Value is LiteralExpressionSyntax lit
                        && lit.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        var constId = lit.Token.ValueText;
                        if (constId != realId)
                        {
                            AddFix(perFileFixes, path,
                                $"DiagnosticId const value {constId} -> {realId}",
                                s => s.Replace($"\"{constId}\"", $"\"{realId}\""));
                        }
                    }
                }
            }
        }

        var totalRenames = classRenames.Count;
        var totalPerFile = perFileFixes.Values.Sum(l => l.Count);
        var totalIssues = totalRenames + totalPerFile;

        if (apply)
        {
            // Apply per-file fixes + global class renames in one pass per file.
            foreach (var path in Directory.EnumerateFiles(analyzersDir, "*.cs", SearchOption.TopDirectoryOnly))
            {
                var src = File.ReadAllText(path);
                var original = src;

                if (perFileFixes.TryGetValue(path, out var fixes))
                    foreach (var (_, applyFn) in fixes)
                        src = applyFn(src);

                foreach (var (oldName, newName) in classRenames)
                    src = Regex.Replace(src, @"\b" + Regex.Escape(oldName) + @"\b", newName);

                if (src != original)
                    File.WriteAllText(path, src);
            }
            Console.WriteLine(
                $"--enforce-ids --apply: {totalRenames} class renames + {totalPerFile} per-file fixes.");
            return 0;
        }

        foreach (var (oldName, newName) in classRenames.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            Console.WriteLine($"  class rename: {oldName} -> {newName}");
        foreach (var (path, fixes) in perFileFixes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var rel = Path.GetRelativePath(repoRoot, path);
            foreach (var (desc, _) in fixes)
                Console.WriteLine($"  {rel}: {desc}");
        }
        Console.WriteLine(
            $"--enforce-ids: {totalIssues} mismatches ({totalRenames} class renames, {totalPerFile} per-file fixes).");
        return totalIssues == 0 ? 0 : 1;
    }

    /// <summary>
    ///   Rewrites the <c>Notes</c> column of
    ///   <c>src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/AnalyzerReleases.Shipped.md</c>
    ///   from free-form rule titles to the Microsoft pattern
    ///   <c>ClassName, [Documentation](url)</c>, where <c>ClassName</c> is the
    ///   <see cref="DiagnosticAnalyzer"/> subclass registering the id and <c>url</c> is
    ///   the <c>HelpLinkUri</c> anchor the descriptor itself uses. Multi-id analyzers
    ///   (e.g., the supplemental QYL0009/0010/0011 family) get the same class name in
    ///   every row, matching MS NetAnalyzers' rendering. RS2008 already enforces that
    ///   rows exist for every descriptor; this rewrite owns the Notes shape.
    /// </summary>
    private static int RewriteShipped(string repoRoot)
    {
        var shippedPath = ShippedReleasesPath(repoRoot);
        if (!File.Exists(shippedPath))
        {
            Console.Error.WriteLine($"Missing Shipped.md: {shippedPath}");
            return 1;
        }
        var existing = File.ReadAllText(shippedPath);
        var expected = RewriteShippedNotes(existing);
        if (string.Equals(existing, expected, StringComparison.Ordinal))
        {
            Console.WriteLine($"Shipped.md Notes already up to date: {Path.GetRelativePath(repoRoot, shippedPath)}");
            return 0;
        }
        File.WriteAllText(shippedPath, expected);
        Console.WriteLine($"Rewrote Shipped.md Notes column: {Path.GetRelativePath(repoRoot, shippedPath)}");
        return 0;
    }

    private static string ShippedReleasesPath(string repoRoot) => Path.Combine(repoRoot,
        "src", "Qyl.OpenTelemetry.SemanticConventions.Analyzers", "AnalyzerReleases.Shipped.md");

    private static string RewriteShippedNotes(string existing)
    {
        var idToClass = BuildIdToClassMap();
        var lineEnding = existing.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = existing.Split([lineEnding], StringSplitOptions.None);
        var rowRx = new Regex(@"^(QYL\d{4})\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*.*$");
        for (var i = 0; i < lines.Length; i++)
        {
            var m = rowRx.Match(lines[i]);
            if (!m.Success) continue;
            var id = m.Groups[1].Value;
            if (!idToClass.TryGetValue(id, out var className)) continue;
            var category = m.Groups[2].Value.Trim();
            var severity = m.Groups[3].Value.Trim();
            var url = AlAnalyzer.HelpLinkBase + id.ToLowerInvariant();
            lines[i] = $"{id} | {category} | {severity} | {className}, [Documentation]({url})";
        }
        return string.Join(lineEnding, lines);
    }

    /// <summary>
    ///   Walks every concrete <see cref="DiagnosticAnalyzer"/> in the analyzer assembly
    ///   and builds <c>Id → ClassName</c>. Analyzers that register multiple ids point
    ///   all of those ids at the same class — matching the
    ///   <c>AL1003ToAl1004SpanComparisonAnalyzer</c> shape in ANcpLua.Analyzers' Shipped.md.
    /// </summary>
    private static Dictionary<string, string> BuildIdToClassMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract) continue;
            if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(type)) continue;
            try
            {
                if (Activator.CreateInstance(type) is DiagnosticAnalyzer a)
                {
                    foreach (var d in a.SupportedDiagnostics)
                        map[d.Id] = type.Name;
                }
            }
            catch { /* analyzers with non-default ctors are skipped */ }
        }
        return map;
    }

    private static (Dictionary<string, string> AnalyzerIds, Dictionary<string, string> CodeFixIds) BuildClassMaps()
    {
        var analyzers = new Dictionary<string, string>(StringComparer.Ordinal);
        var codeFixes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            if (typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            {
                try
                {
                    if (Activator.CreateInstance(type) is DiagnosticAnalyzer a && a.SupportedDiagnostics.Length > 0)
                        analyzers[type.Name] = a.SupportedDiagnostics
                            .OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;
                }
                catch { /* analyzers with non-default ctors are skipped */ }
            }
            else if (typeof(CodeFixProvider).IsAssignableFrom(type))
            {
                try
                {
                    if (Activator.CreateInstance(type) is CodeFixProvider p && p.FixableDiagnosticIds.Length > 0)
                        codeFixes[type.Name] = p.FixableDiagnosticIds
                            .OrderBy(s => s, StringComparer.Ordinal).First();
                }
                catch { }
            }
        }
        return (analyzers, codeFixes);
    }

    private static void AddFix(
        Dictionary<string, List<(string Description, Func<string, string> Apply)>> bucket,
        string path,
        string description,
        Func<string, string> apply)
    {
        if (!bucket.TryGetValue(path, out var list))
        {
            list = [];
            bucket[path] = list;
        }
        list.Add((description, apply));
    }

    private static string RenderMarkdown(CatalogStatistics stats)
    {
        var descriptors = GetDescriptors();
        var fixableIds = GetFixableDiagnosticIds();
        var sb = new StringBuilder();

        var sections = new Action<StringBuilder>[]
        {
            WriteHeader,
            b => WriteDiagnostics(b, descriptors, fixableIds),
            b => WriteDiagnosticAnchors(b, descriptors, fixableIds),
            WritePrecedenceAndSuppression,
            WriteSeverityPolicy,
            WriteConfiguration,
            b => WriteCuratedSummary(b, stats),
            b => WriteCompletionAudit(b, stats),
            b => WriteVersionDomainTable(b, stats),
            b => WriteCuratedInventory(b, stats),
            b => WriteSupplementalValues(b, stats),
            WriteGeneratedFile,
        };

        foreach (var section in sections)
        {
            if (sb.Length > 0) sb.AppendLine();
            section(sb);
        }

        return sb.ToString().ReplaceLineEndings("\n");
    }

    private static void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine($"# {PackageName}");
        sb.AppendLine();
        sb.AppendLine($"<!-- <auto-generated /> This file is generated by {ProjectRelativePath}. -->");
        sb.AppendLine();
        sb.AppendLine("This package analyzes OpenTelemetry semantic-convention usage in C# consumers. The consumer's referenced `OpenTelemetry.SemanticConventions` assembly remains the primary source of truth: rules that read `[Obsolete]` metadata report what that package actually generated. The curated inventory below separates live-metadata coverage from supplemental diagnostics for changelog/model entries that are not reliably visible through live metadata.");
        sb.AppendLine();
        sb.AppendLine("## Package family");
        sb.AppendLine();
        sb.AppendLine("- **[Qyl OTel-conventions repo](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions)** — this analyzer (`QYL00xx` rules), the stable + incubating attribute-key packages, and the source generator that emits typed `Activity`/`Event`/`Meter`/`Metric` projections.");
        sb.AppendLine("- **[ANcpLua framework](https://www.nuget.org/profiles/ANcpLua)** — upstream Roslyn infrastructure consumed by this package: `ANcpLua.Roslyn.Utilities` (shared helpers + `Guard.*` API), `ANcpLua.Analyzers` (general-purpose `AL00xx` band), `ANcpLua.NET.Sdk` (MSBuild SDK that auto-injects the framework's analyzers + `.editorconfig` defaults), `ANcpLua.Agents` (Microsoft Agent Framework toolkit). `QYL00xx` rules are scoped to OTel-conventions consumers; framework rules live in `ANcpLua.Analyzers`.");
    }

    private static void WriteDiagnostics(
        StringBuilder sb,
        IReadOnlyList<DiagnosticDescriptor> descriptors,
        HashSet<string> fixableIds)
    {
        sb.AppendLine("## Diagnostics");
        sb.AppendLine();
        sb.AppendLine("| ID | Severity | Title | Code fix | Description |");
        sb.AppendLine("| -- | -- | -- | -- | -- |");
        foreach (var d in descriptors)
        {
            var codeFix = GetCodeFixLabel(d.Id, fixableIds);
            sb.AppendLine($"| {d.Id} | {d.DefaultSeverity} | {Escape(d.Title.ToString())} | {codeFix} | {Escape(d.Description.ToString())} |");
        }
    }

    private static void WriteDiagnosticAnchors(
        StringBuilder sb,
        IReadOnlyList<DiagnosticDescriptor> descriptors,
        HashSet<string> fixableIds)
    {
        sb.AppendLine("## Rule Reference");
        sb.AppendLine();
        sb.AppendLine("Each rule below has a stable GitHub anchor (`#qyl0010`, `#qyl0011`, …) that every `DiagnosticDescriptor.HelpLinkUri` resolves to. Quick-fix \"Show error help\" links and IDE diagnostic tooltips deep-link straight to the matching sub-section.");
        sb.AppendLine();
        foreach (var d in descriptors)
        {
            sb.AppendLine($"### {d.Id}");
            sb.AppendLine();
            sb.AppendLine($"**{Escape(d.Title.ToString())}** — *{d.DefaultSeverity}, category `{d.Category}`*");
            sb.AppendLine();
            sb.AppendLine(Escape(d.Description.ToString()));
            sb.AppendLine();
            sb.AppendLine($"Code fix: {GetCodeFixLabel(d.Id, fixableIds)}.");
            sb.AppendLine();
        }
    }

    private static string GetCodeFixLabel(string diagnosticId, HashSet<string> fixableIds)
    {
        if (!fixableIds.Contains(diagnosticId))
        {
            return "No";
        }

        // QYL0009/QYL0010/QYL0011 share SupplementalSemconvMigrationCodeFixProvider,
        // which gates registration via IsExactReplacement (MigrationKind == ExactRename
        // / ExactValueRename) per-diagnostic, not by ID. Reflect that contract in docs.
        return diagnosticId is "QYL0009" or "QYL0010" or "QYL0011"
            ? "Exact replacements only"
            : "Yes";
    }

    private static void WritePrecedenceAndSuppression(StringBuilder sb)
    {
        sb.AppendLine("## Precedence and Suppression");
        sb.AppendLine();
        sb.AppendLine("**Live metadata wins over the supplemental catalog.** When the consumer's referenced `OpenTelemetry.SemanticConventions` package marks a constant or value `[Obsolete]`, the supplemental catalog diagnostics (`QYL0009`/`QYL0010`/`QYL0011`) skip that symbol entirely — only the live-metadata rules (`QYL0003`/`QYL0005`/`QYL0007`) fire. No symbol produces two diagnostics for the same root cause.");
        sb.AppendLine();
        sb.AppendLine("**Multi-hop renames resolve to the terminal symbol.** `SemconvMigrationCatalog.ResolveTerminalReplacement` walks `ExactRename` / `ExactValueRename` chains so a code fix on `http.host → net.host.name → server.address` lands consumers on `server.address`, not on the still-deprecated `net.host.name` mid-state. Cycles and chains over 8 hops bail at the last safe step.");
        sb.AppendLine();
        sb.AppendLine("**Per-type suppressor for legacy shapes.** `SemconvLegacyContextSuppressor` recognises class/struct/record/method names matching well-known compatibility shapes (`Legacy*`, `*CompatShim`, `*MigrationFixture`, `*SchemaTranslator`, `*DeprecatedSemconv*`) and reports `Suppression`s for every QYL* diagnostic inside them — no `#pragma` walls required. Pair it with `build_property.OtelSemConvLegacyMode = compatibility` when the *whole project* is a translator, and use the suppressor when only specific types intentionally emit older schemas inside an otherwise production project.");
        sb.AppendLine();
        sb.AppendLine("**Structured provenance per catalog entry.** Each `SemconvMigrationCatalogEntry` carries an optional `SemconvChangelogEvidence` (commit / version / url / quote) pinning the claim to an upstream commit.");
        sb.AppendLine();
    }

    private static void WriteSeverityPolicy(StringBuilder sb)
    {
        sb.AppendLine("## Severity Policy");
        sb.AppendLine();
        sb.AppendLine("- `QYL0003`, `QYL0005`, and `QYL0007` read `[Obsolete]` metadata from the referenced semantic-conventions assembly and keep their descriptor severities.");
        sb.AppendLine("- `QYL0009` is reserved for production telemetry emission where a supplemental catalog item has an exact one-to-one replacement, including exact attribute-value replacements when live metadata is absent.");
        sb.AppendLine("- `QYL0010` is used for context-sensitive migrations, removed/no-replacement entries, guidance-only cases, ambiguous payload dictionaries, and `compatibility` mode downgrades.");
        sb.AppendLine("- `QYL0011` is used for tests, fixtures, snapshots, migration maps, schema translators, compatibility shims, generated sources, and catalog-like code.");
        sb.AppendLine("- Generated semconv constant libraries may intentionally retain deprecated constants. Their existence is not itself a package bug.");
        sb.AppendLine("- Schema URL translators and code that explicitly emits older schemas are compatibility contexts and should not be escalated to production errors.");
    }

    private static void WriteConfiguration(StringBuilder sb)
    {
        sb.AppendLine("## Configuration");
        sb.AppendLine();
        sb.AppendLine("| Option | Values | Behavior |");
        sb.AppendLine("| -- | -- | -- |");
        sb.AppendLine("| `build_property.OtelSemConvLegacyMode` | `production` (default), `compatibility`, `off` | `production` keeps production errors for exact supplemental migrations. `compatibility` downgrades production supplemental errors to warnings and keeps fixture contexts informational. `off` disables supplemental catalog diagnostics while leaving live `[Obsolete]` metadata rules enabled. |");
        sb.AppendLine("| `build_property.IsTestProject` | `true`, `false` | Test projects downgrade supplemental catalog findings to `QYL0011` info. Assembly names ending in `.Tests`, paths under `tests/`, and xUnit/NUnit/MSTest attributes are also treated as test context. |");
        sb.AppendLine("| `build_property.OtelSemConvNonAttributesTiers` | `false` (default), `true` | When `true`, extends `QYL0003` beyond `*Attributes` classes to also scan the four other Weaver source-generation tiers (`*Metrics`, `*Meters`, `*Events`, `*Activities`) under the SemConv namespace. Default `false` preserves the historic surface so existing consumers see no behaviour change. |");
    }

    // Per-rule examples will land alongside per-rule docs pages in the next refactor.
    // The previous monolithic Examples section referenced legacy rule numbers (QYL0014,
    // QYL0030, QYL0031, QYL0032 — none of which exist in the current Shipped.md band)
    // and was actively misleading. Removed until step 4 of the docs plan ships per-rule
    // markdown pages with examples sourced from the actual analyzer fixtures.

    private static void WriteCuratedSummary(StringBuilder sb, CatalogStatistics stats)
    {
        sb.AppendLine("## Curated Migration Inventory Summary");
        sb.AppendLine();
        sb.AppendLine($"Curated changelog mentions: {stats.Entries.Length}. Live metadata rows: {stats.Metadata}. Supplemental diagnostic rows: {stats.Supplemental}. Exact supplemental replacements: {stats.Exact}. Manual/context-sensitive supplemental rows: {stats.Manual}. Removed/no-replacement supplemental rows: {stats.Removed}. Guidance-only rows: {stats.Guidance}.");
        sb.AppendLine($"Supplemental attribute-value fallback rows: {stats.ValueEntries.Length}. Exact value replacements: {stats.ExactValue}. Manual value rows: {stats.ManualValue}. Removed/no-replacement value rows: {stats.RemovedValue}. These rows are used only when the same key/value is not covered by live `[Obsolete]` metadata from the referenced package.");
    }

    private static void WriteCompletionAudit(StringBuilder sb, CatalogStatistics stats)
    {
        sb.AppendLine("## Completion Audit");
        sb.AppendLine();
        sb.AppendLine("This section is generated from the analyzer descriptors and migration catalog. It is intended to make the package-level completion claim reviewable without hand-counting the catalog.");
        sb.AppendLine();
        sb.AppendLine("| Requirement | Current generated evidence |");
        sb.AppendLine("| -- | -- |");
        sb.AppendLine($"| Preserve the 156 curated changelog-entry scope | `SemconvMigrationCatalog.Validate()` requires exactly `{SemconvMigrationCatalog.ExpectedCuratedMentionCount}` curated rows; current generated count is `{stats.Entries.Length}`. |");
        sb.AppendLine($"| Prefer live `[Obsolete]` metadata where available | `{stats.Metadata}` of `{stats.Entries.Length}` curated rows are classified as `DeprecatedButGenerated`; `QYL0003`, `QYL0005`, and `QYL0007` remain the live-metadata diagnostics. |");
        sb.AppendLine($"| Use supplemental diagnostics only where metadata is insufficient | `{stats.Supplemental}` curated rows are supplemental diagnostics: `{stats.Exact}` exact replacement, `{stats.Manual}` manual/context-sensitive, `{stats.Removed}` removed/no-replacement, `{stats.Guidance}` guidance-only. |");
        sb.AppendLine($"| Keep attribute-value fallback separate from the curated name/key/event/metric count | `{stats.ValueEntries.Length}` supplemental attribute-value rows are outside the 156-entry inventory and are used only when live value metadata is absent. |");
        sb.AppendLine("| Keep severity context-sensitive | `QYL0009` is production exact replacement error, `QYL0010` is production manual-review warning, and `QYL0011` is compatibility/test/generated info. |");
        sb.AppendLine("| Keep code fixes exact-only | `LiveSemconvMetadataCodeFixProvider` registers fixes only when live `[Obsolete]` metadata exposes an exact replacement; `SupplementalSemconvMigrationCodeFixProvider` registers fixes only when diagnostic properties mark `ExactRename` or `ExactValueRename` and provide one replacement literal. |");
        sb.AppendLine("| Keep old-schema compatibility non-error | Test, fixture, migration, compatibility, translator, generated, catalog, and explicit older schema URL contexts select `QYL0011`. |");
        sb.AppendLine();
        sb.AppendLine("| Migration kind | Curated count |");
        sb.AppendLine("| -- | --: |");
        foreach (var g in stats.Entries
            .GroupBy(e => e.MigrationKind)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            sb.AppendLine($"| {g.Key} | {g.Count()} |");
        }
        sb.AppendLine();
        sb.AppendLine("| Item kind | Curated count |");
        sb.AppendLine("| -- | --: |");
        foreach (var g in stats.Entries
            .GroupBy(e => e.Kind)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal))
        {
            sb.AppendLine($"| {g.Key} | {g.Count()} |");
        }
        sb.AppendLine();
        sb.AppendLine("| Production surface | Analyzer coverage evidence |");
        sb.AppendLine("| -- | -- |");
        sb.AppendLine("| `Activity.SetTag` / `AddTag` and baggage-like calls | Shared payload detection covers known tag setters plus `SetBaggage`/`AddBaggage` for live metadata and supplemental catalog checks. |");
        sb.AppendLine("| `TagList` / `ActivityTagsCollection` | `Add` payloads and `ActivityTagsCollection` indexer writes are recognized. |");
        sb.AppendLine("| Inline and local attribute payload collections | Arrays, object/collection initializers, C# collection expressions, `KeyValuePair<string, object?>`, local dictionary initializers, and local mutable dictionary `Add`/indexer writes are recognized when they visibly flow to telemetry APIs. |");
        sb.AppendLine("| Span/event/link/resource payloads | `ActivitySource.StartActivity(tags:)`, `ActivityEvent` tags, `ActivityLink` tags, and `ResourceBuilder.AddAttributes` are recognized. |");
        sb.AppendLine("| Metric payloads and names | `Counter<T>.Add`, `Histogram<T>.Record`, `UpDownCounter<T>.Add`, `Measurement<T>` tags, and `Meter.CreateCounter/Histogram/Gauge/Observable*` names are recognized. |");
        sb.AppendLine("| Logging payloads | Visible `ILogger.Log` state and `ILogger.BeginScope` state payloads are recognized when the key/value is statically visible. |");
    }

    private static void WriteVersionDomainTable(StringBuilder sb, CatalogStatistics stats)
    {
        sb.AppendLine("## Coverage by Version × Domain");
        sb.AppendLine();
        sb.AppendLine("Rows whose Version is `unknown` are catalog entries that don't yet carry `ChangelogVersion` or `SinceVersion` fields in `SemconvMigrationCatalog.BuildEntries()`. Backfilling these from upstream `open-telemetry/semantic-conventions` CHANGELOG.md is a separate hardening task; the analyzers still surface the migrations correctly — only the per-version attribution is missing.");
        sb.AppendLine();
        sb.AppendLine("| Version | Domain | Total | Live metadata | Supplemental | Exact supplemental | Manual/context | Removed/no replacement |");
        sb.AppendLine("| -- | -- | --: | --: | --: | --: | --: | --: |");
        foreach (var g in stats.Entries
            .GroupBy(e => (Version: NormalizeVersion(e.ChangelogVersion, e.SinceVersion), e.Domain))
            .OrderByDescending(g => VersionSortKey(g.Key.Version), StringComparer.Ordinal)
            .ThenBy(g => g.Key.Domain, StringComparer.Ordinal))
        {
            var rows = g.ToArray();
            var live = rows.Count(e => e.MigrationKind == SemconvMigrationKind.DeprecatedButGenerated);
            var supp = rows.Count(SemconvMigrationCatalog.IsSupplementalDiagnosticEntry);
            var exact = rows.Count(e => SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(e)
                && e.MigrationKind is SemconvMigrationKind.ExactRename or SemconvMigrationKind.ExactValueRename);
            var manual = rows.Count(e => SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(e)
                && e.MigrationKind is SemconvMigrationKind.ContextSensitive or SemconvMigrationKind.ManualReview);
            var removed = rows.Count(e => SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(e)
                && e.MigrationKind == SemconvMigrationKind.RemovedNoReplacement);
            sb.AppendLine($"| {g.Key.Version} | {g.Key.Domain} | {rows.Length} | {live} | {supp} | {exact} | {manual} | {removed} |");
        }
    }

    private static void WriteCuratedInventory(StringBuilder sb, CatalogStatistics stats)
    {
        sb.AppendLine("## Curated Migration Inventory");
        sb.AppendLine();
        sb.AppendLine("| Old name | Kind | Signal | Domain | Since | Migration | Replacement | Evidence |");
        sb.AppendLine("| -- | -- | -- | -- | -- | -- | -- | -- |");
        foreach (var e in stats.Entries
            .OrderBy(x => x.Domain, StringComparer.Ordinal)
            .ThenBy(x => x.OldName, StringComparer.Ordinal))
        {
            var since = string.IsNullOrEmpty(e.SinceVersion) ? "-" : Escape(e.SinceVersion);
            var replacement = FormatReplacement(e.ReplacementNames);
            sb.AppendLine($"| `{e.OldName}` | {e.Kind} | {e.Signal} | {e.Domain} | {since} | {e.MigrationKind} | {replacement} | {Escape(e.ChangelogEvidence)} |");
        }
    }

    private static void WriteSupplementalValues(StringBuilder sb, CatalogStatistics stats)
    {
        sb.AppendLine("## Supplemental Attribute Value Fallback");
        sb.AppendLine();
        sb.AppendLine("These value rows are intentionally separate from the 156-entry name/key/event/metric inventory. `QYL0007` remains primary when the referenced package exposes `[Obsolete]` value constants; the supplemental analyzer uses this table only when live value metadata is absent.");
        sb.AppendLine();
        sb.AppendLine("| Old value | Signal | Domain | Migration | Replacement | Evidence |");
        sb.AppendLine("| -- | -- | -- | -- | -- | -- |");
        foreach (var e in stats.ValueEntries
            .OrderBy(x => x.Domain, StringComparer.Ordinal)
            .ThenBy(x => x.OldName, StringComparer.Ordinal))
        {
            var replacement = FormatReplacement(e.ReplacementNames);
            sb.AppendLine($"| `{e.OldName}` | {e.Signal} | {e.Domain} | {e.MigrationKind} | {replacement} | {Escape(e.ChangelogEvidence)} |");
        }
    }

    private static void WriteGeneratedFile(StringBuilder sb)
    {
        sb.AppendLine("## Generated File");
        sb.AppendLine();
        sb.AppendLine("Regenerate with:");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("./build.sh GenerateDocs");
        sb.AppendLine("./build.sh CheckDocs    # fails if the committed markdown is stale");
        sb.AppendLine("./build.sh AuditDocs    # prints catalog statistics, no file I/O");
        sb.AppendLine("```");
    }

    private static IReadOnlyList<DiagnosticDescriptor> GetDescriptors()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var descriptors = new List<DiagnosticDescriptor>();

        // (1) Centralised DiagnosticDescriptors.cs fields.
        foreach (var field in typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor)))
        {
            if (field.GetValue(null) is DiagnosticDescriptor d && seen.Add(d.Id))
                descriptors.Add(d);
        }

        // (2) Per-analyzer SupportedDiagnostics for QYL00XX-named analyzers that define
        //     their descriptor locally (no entry in DiagnosticDescriptors.cs).
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                continue;

            DiagnosticAnalyzer? analyzer;
            try { analyzer = (DiagnosticAnalyzer?)Activator.CreateInstance(type); }
            catch { continue; }
            if (analyzer is null) continue;

            foreach (var d in analyzer.SupportedDiagnostics)
            {
                if (seen.Add(d.Id))
                    descriptors.Add(d);
            }
        }

        return descriptors
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> GetFixableDiagnosticIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CodeFixProvider).IsAssignableFrom(type))
            {
                continue;
            }

            CodeFixProvider? provider;
            try
            {
                provider = (CodeFixProvider?)Activator.CreateInstance(type);
            }
            catch
            {
                continue;
            }

            if (provider is null)
            {
                continue;
            }

            foreach (var id in provider.FixableDiagnosticIds)
            {
                if (id.StartsWith("QYL", StringComparison.Ordinal))
                {
                    ids.Add(id);
                }
            }
        }
        return ids;
    }

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not find repository root (no '{SolutionFileName}' in any parent of '{start}').");
    }

    private static string NormalizeVersion(string changelogVersion, string sinceVersion) =>
        !string.IsNullOrWhiteSpace(changelogVersion) ? changelogVersion
        : !string.IsNullOrWhiteSpace(sinceVersion) ? sinceVersion
        : "unspecified";

    private static string VersionSortKey(string version) =>
        version is "unspecified" or "unknown" ? "0.0.0" : version;

    private static string FormatReplacement(ImmutableArray<string> names) =>
        names.Length == 0
            ? "-"
            : Escape(string.Join(", ", names.Select(n => "`" + n + "`")));

    private static string Escape(string value) =>
        value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal);
}

file enum Mode { Generate, Check, Audit, EnforceIdsCheck, EnforceIdsApply, RewriteShipped }

file readonly record struct CatalogStatistics(
    ImmutableArray<SemconvMigrationCatalogEntry> Entries,
    ImmutableArray<SemconvMigrationCatalogEntry> ValueEntries,
    int Metadata,
    int Supplemental,
    int Exact,
    int Manual,
    int Removed,
    int Guidance,
    int ExactValue,
    int ManualValue,
    int RemovedValue)
{
    public static CatalogStatistics Compute()
    {
        SemconvMigrationCatalog.Validate();
        var entries = SemconvMigrationCatalog.Entries;
        var valueEntries = SemconvMigrationCatalog.SupplementalAttributeValueEntries;
        var supplemental = entries.Where(SemconvMigrationCatalog.IsSupplementalDiagnosticEntry).ToArray();

        return new CatalogStatistics(
            Entries: entries,
            ValueEntries: valueEntries,
            Metadata: entries.Count(e => e.MigrationKind == SemconvMigrationKind.DeprecatedButGenerated),
            Supplemental: supplemental.Length,
            Exact: supplemental.Count(e => e.MigrationKind is SemconvMigrationKind.ExactRename or SemconvMigrationKind.ExactValueRename),
            Manual: supplemental.Count(e => e.MigrationKind is SemconvMigrationKind.ContextSensitive or SemconvMigrationKind.ManualReview),
            Removed: supplemental.Count(e => e.MigrationKind == SemconvMigrationKind.RemovedNoReplacement),
            Guidance: entries.Count(e => e.Kind == SemconvMigrationItemKind.GuidanceOnly),
            ExactValue: valueEntries.Count(e => e.MigrationKind == SemconvMigrationKind.ExactValueRename),
            ManualValue: valueEntries.Count(e => e.MigrationKind == SemconvMigrationKind.ManualReview),
            RemovedValue: valueEntries.Count(e => e.MigrationKind == SemconvMigrationKind.RemovedNoReplacement));
    }

    public string RenderAudit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("OpenTelemetry semantic-convention migration audit");
        sb.AppendLine($"Curated changelog mentions: {Entries.Length}");
        sb.AppendLine($"Live [Obsolete] metadata rows: {Metadata}");
        sb.AppendLine($"Supplemental diagnostic rows: {Supplemental}");
        sb.AppendLine($"Exact supplemental replacements: {Exact}");
        sb.AppendLine($"Manual/context-sensitive supplemental rows: {Manual}");
        sb.AppendLine($"Removed/no-replacement supplemental rows: {Removed}");
        sb.AppendLine($"Guidance-only rows: {Guidance}");
        sb.AppendLine($"Supplemental attribute-value fallback rows outside the 156 count: {ValueEntries.Length}");
        return sb.ToString();
    }
}
