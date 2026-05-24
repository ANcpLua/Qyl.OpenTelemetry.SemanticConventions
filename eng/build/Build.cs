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
using Qyl.OpenTelemetry.SemanticConventions.Nuke;
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
    ///   legitimate regeneration. Also exercises
    ///   <see cref="LockstepPolicy.ParseSemconvSuffixVersion"/> on the configured
    ///   <c>{SemconvVersion}-{LockstepRevision}</c> pair as a smoke-check that the
    ///   shipped Nuke component is wired in correctly.
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
