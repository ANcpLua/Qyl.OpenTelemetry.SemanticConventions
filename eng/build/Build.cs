// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Qyl.OpenTelemetry.SemanticConventions.Build;

/// <summary>
///   Repository-local build host. VerifyAttributesHash guards the committed
///   generated attribute files against untracked edits.
/// </summary>
internal sealed class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution(GenerateProjects = false)]
    readonly Solution Solution = null!;

    AbsolutePath AttributesHashFile => RootDirectory / "eng" / "semconv" / "attributes.lock.sha256";

    AbsolutePath StableAttributesDir =>
        RootDirectory / "src" / "Qyl.OpenTelemetry.SemanticConventions" / "Attributes";

    AbsolutePath IncubatingAttributesDir =>
        RootDirectory / "src" / "Qyl.OpenTelemetry.SemanticConventions.Incubating" / "Attributes";

    AbsolutePath DocsGeneratorProject =>
        RootDirectory / "tools"
        / "Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator"
        / "Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator.csproj";

    /// <summary>Restore + compile every project in the solution.</summary>
    Target Compile => _ => _
        .Executes(() =>
        {
            DotNetTasks.DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration("Release")
                .EnableNoLogo());
        });

    /// <summary>
    ///   Hash the committed Weaver-emitted attribute files and compare against the
    ///   manifest at <c>eng/semconv/attributes.lock.sha256</c>. Fails the build if
    ///   anyone hand-edited a generated file or forgot to re-seed the lock after a
    ///   legitimate regeneration.
    /// </summary>
    Target VerifyAttributesHash => _ => _
        .Executes(() =>
        {
            string actual = ComputeAttributesManifestHash();

            if (!File.Exists(AttributesHashFile))
            {
                throw new InvalidOperationException(
                    $"VerifyAttributesHash: lock file not found at {AttributesHashFile}. " +
                    "Run `./build.sh SeedAttributesHash` once to create it.");
            }

            string expected = File.ReadAllText(AttributesHashFile).Trim();
            if (string.Equals(expected, actual, StringComparison.Ordinal))
            {
                Log.Information("VerifyAttributesHash: manifest hash matches ({Hash}).", actual);
                return;
            }

            throw new InvalidOperationException(
                "VerifyAttributesHash: drift detected. " +
                $"Expected {expected}, got {actual}. " +
                "Either re-run `./build.sh SeedAttributesHash` after an intentional regen, " +
                "or revert hand-edits to the generated .g.cs files.");
        });

    /// <summary>Recompute and persist the attribute-manifest hash. Run after intentional regenerations.</summary>
    Target SeedAttributesHash => _ => _
        .Executes(() =>
        {
            string hash = ComputeAttributesManifestHash();
            AttributesHashFile.Parent!.CreateDirectory();
            File.WriteAllText(AttributesHashFile, hash + Environment.NewLine);
            Log.Information("SeedAttributesHash: wrote {Hash} to {Path}.", hash, AttributesHashFile);
        });

    /// <summary>
    ///   Regenerate the analyzer index, per-rule pages, migration catalog, SARIF, and
    ///   editorconfig profiles from <c>DiagnosticDescriptors</c> and
    ///   <c>SemconvMigrationCatalog</c>. Every QYL rule's <c>HelpLinkUri</c> resolves to its
    ///   generated per-rule page.
    /// </summary>
    Target GenerateDocs => _ => _
        .Description("Regenerate analyzer documentation and machine-readable rule artifacts.")
        .Executes(() => RunDocsGenerator(applicationArguments: null, buildGenerator: true));

    /// <summary>CI guard: fail when the committed markdown drifts from what the generator would emit now.</summary>
    Target CheckDocs => _ => _
        .Description("Fail if docs are stale relative to the analyzer assembly.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator("--check"));

    /// <summary>Print catalog statistics (curated vs supplemental, fixable counts, ...). No file I/O.</summary>
    Target AuditDocs => _ => _
        .Description("Print analyzer catalog statistics.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator("--audit"));

    /// <summary>
    ///   Read-only consistency check: every analyzer .cs file's class name, class XML doc summary,
    ///   and DiagnosticId const must agree with the runtime <c>DiagnosticDescriptor.Id</c> it
    ///   registers. Fails the build on mismatches without touching files.
    /// </summary>
    Target EnforceIds => _ => _
        .Description("Verify analyzer class/doc/id-const consistency against runtime descriptors.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator("--enforce-ids"));

    /// <summary>
    ///   Rewrite analyzer source files in-place to make class names, class XML doc summaries, and
    ///   DiagnosticId-const docs match the runtime <c>DiagnosticDescriptor.Id</c> they register.
    ///   Run after an intentional rule-id change; review the diff before committing.
    /// </summary>
    Target EnforceIdsApply => _ => _
        .Description("Rewrite analyzer files to align class/doc/id-const with runtime descriptors.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator("--enforce-ids --apply"));

    void RunDocsGenerator(string? applicationArguments, bool buildGenerator = false)
    {
        var settings = new DotNetRunSettings()
            .SetProjectFile(DocsGeneratorProject)
            .SetConfiguration("Release");

        settings = buildGenerator
            ? settings.SetProcessEnvironmentVariable("_QylInsideConsistencyCheck", "true")
            : settings.EnableNoBuild().EnableNoRestore();

        if (applicationArguments is not null)
            settings = settings.SetApplicationArguments(applicationArguments);

        DotNetTasks.DotNetRun(settings);
        Log.Information("DocsGenerator finished ({Mode}).", applicationArguments ?? "generate");
    }

    string ComputeAttributesManifestHash()
    {
        var entries = new List<(string RelPath, string Sha)>();
        foreach (AbsolutePath root in new[] { StableAttributesDir, IncubatingAttributesDir })
        {
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.EnumerateFiles(root, "*.g.cs", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(RootDirectory, file).Replace('\\', '/');
                using FileStream stream = File.OpenRead(file);
                string sha = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                entries.Add((relPath, sha));
            }
        }

        // Sort by path so the manifest is order-independent.
        entries.Sort(static (a, b) => string.CompareOrdinal(a.RelPath, b.RelPath));

        var manifest = new StringBuilder();
        foreach ((string relPath, string sha) in entries)
            manifest.Append(sha).Append("  ").Append(relPath).Append('\n');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())))
            .ToLowerInvariant();
    }
}
