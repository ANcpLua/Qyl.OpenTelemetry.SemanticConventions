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
using ANcpLua.OpenTelemetry.Conventions.Nuke;
using Serilog;

namespace Qyl.OpenTelemetry.SemanticConventions.Build;

/// <summary>
///   Monorepo build host. Dogfoods Qyl.OpenTelemetry.SemanticConventions.Nuke by
///   implementing IUpstreamConventions on the same package consumers will install
///   from nuget.org, and by exercising LockstepPolicy + the helper machinery in a
///   VerifyAttributesHash target that guards the committed .g.cs files against
///   undetected hand-edits.
/// </summary>
internal sealed class Build : NukeBuild, IUpstreamConventions
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution(GenerateProjects = false)]
    readonly Solution Solution = null!;

    [Parameter("Generator-revision counter for lockstep validation ({semconv}-{n}, default 1).")]
    readonly int LockstepRevision = 1;

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
    ///   legitimate regeneration. Also parses the configured
    ///   <c>{SemconvVersion}-{LockstepRevision}</c> pair with
    ///   <see cref="LockstepPolicy.ParseSemconvSuffixVersion"/> and logs the result;
    ///   this exercises the type binding from the shipped Nuke component but does
    ///   not itself assert on the parsed values.
    /// </summary>
    Target VerifyAttributesHash => _ => _
        .Executes(() =>
        {
            string lockstep = $"{((IUpstreamConventions)this).SemconvVersion}-{LockstepRevision}";
            (string semconv, int n) = LockstepPolicy.ParseSemconvSuffixVersion(lockstep);
            Log.Information(
                "VerifyAttributesHash: lockstep version {Lockstep} parsed as semconv={Semconv} n={N}.",
                lockstep, semconv, n);

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
    ///   Re-render <c>docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md</c> from the
    ///   analyzer assembly's <c>DiagnosticDescriptors</c> + <c>SemconvMigrationCatalog</c>.
    ///   Every QYL rule's <c>HelpLinkUri</c> deep-links into a <c>### QYL00XX</c> sub-section
    ///   of that file, so the generator output is the contract the descriptors anchor into.
    /// </summary>
    Target GenerateDocs => _ => _
        .Description("Re-render docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md from the analyzer assembly.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator(applicationArguments: null));

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

    void RunDocsGenerator(string? applicationArguments)
    {
        var settings = new DotNetRunSettings()
            .SetProjectFile(DocsGeneratorProject)
            .SetConfiguration("Release")
            .EnableNoBuild()
            .EnableNoRestore();

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
