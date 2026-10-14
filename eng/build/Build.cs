// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using DotCov.Fallout;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Serilog;

namespace Qyl.Telemetry.SemanticConventions.Build;

/// <summary>
///   Repository-local build host: compile, the coverage gate, and the analyzer documentation
///   targets. The packages' generated surface is committed and guarded by
///   scripts/check-generated.sh, so nothing here regenerates it.
/// </summary>
internal sealed partial class Build : FalloutBuild, ICoverageReport
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution(GenerateProjects = false)]
    readonly Solution Solution = null!;

    AbsolutePath DocsGeneratorProject =>
        RootDirectory / "tools"
        / "Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator"
        / "Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator.csproj";

    AbsolutePath PipelineTestsProject =>
        RootDirectory / "tests"
        / "Qyl.Telemetry.SemanticConventions.Pipeline.Tests"
        / "Qyl.Telemetry.SemanticConventions.Pipeline.Tests.csproj";

    AbsolutePath CoverageDirectory => RootDirectory / "artifacts" / "coverage";

    // The coverage gate's thresholds sit a few points under what the pipeline tests measured when
    // the gate was added, test code excluded: line 22.6 %, branch 14.9 %. The untested generated
    // helpers in the Incubating package (Spans, Metrics, Events, Entities) hold the numbers down.
    // --coverage-min-line and --coverage-min-branch override a threshold for one run.
    string? ICoverageReport.MinLine => ((ICoverageReport)this).TryGetValue(() => ((ICoverageReport)this).MinLine) ?? "20";
    string? ICoverageReport.MinBranch => ((ICoverageReport)this).TryGetValue(() => ((ICoverageReport)this).MinBranch) ?? "12";
    string ICoverageReport.ExcludeTestCodeParam => "true";
    AbsolutePath ICoverageReport.CoverageSearchDirectory => CoverageDirectory;

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
    ///   Run the pipeline tests under Microsoft code coverage into a clean artifacts/coverage,
    ///   then let dotcov's ReportCoverage gate the Cobertura report against the thresholds above.
    /// </summary>
    Target Coverage => _ => _
        .Description("Run the pipeline tests with coverage and gate line and branch coverage.")
        .DependsOn(Compile)
        .Triggers<ICoverageReport>(x => x.ReportCoverage)
        .Executes(() =>
        {
            CoverageDirectory.CreateOrCleanDirectory();
            DotNetTasks.DotNetRun(s => s
                .SetProjectFile(PipelineTestsProject)
                .SetConfiguration("Release")
                .EnableNoBuild()
                .SetApplicationArguments(
                    "--coverage", "--coverage-output-format", "cobertura",
                    "--coverage-output", "coverage.cobertura.xml",
                    "--results-directory", CoverageDirectory));
        });

    [Parameter("VerifyRelease: the released version to verify on nuget.org and npm")]
    readonly string? PublishedVersion = null;  // Fallout injects it

    static readonly string[] NuGetPackageIds =
    [
        "Qyl.Telemetry.SemanticConventions",
        "Qyl.Telemetry.SemanticConventions.Analyzers",
        "Qyl.Telemetry.SemanticConventions.Incubating",
    ];

    /// <summary>
    ///   release.sh's detached verification, rerunnable by hand after a patch release: wait until
    ///   nuget.org and npm serve the version (ReleaseIndex, up to 45 minutes), then the release
    ///   smoke against the registries, eng/release/verify-packages.sh (a clean NuGet consumer of all
    ///   three packages) and eng/release/verify-typescript.sh. The expected registry fingerprint is
    ///   read from the release tag, not the working tree, which may hold the next version.
    /// </summary>
    Target VerifyRelease => _ => _
        .Description("Wait until nuget.org and npm serve --published-version, then smoke the registry packages.")
        .Requires(() => PublishedVersion)
        .Executes(() =>
        {
            var version = PublishedVersion!;
            ReleaseIndex.Wait(
                ReleaseIndex.StatusFile(RootDirectory, version),
                TimeSpan.FromMinutes(45),
                [.. NuGetPackageIds.Select(id => ReleaseIndex.NuGet(id, version)), ReleaseIndex.Npm("@ancplua/qyl-semantic-conventions", version)]);

            var tagged = ProcessTasks.StartProcess("git", $"show v{version}:typescript/src/index.ts", RootDirectory, logOutput: false)
                .AssertZeroExitCode().Output.Select(line => line.Text);
            var digest = tagged.Select(line => RegistryDigest().Match(line)).FirstOrDefault(match => match.Success)?.Groups[1].Value
                ?? throw new InvalidOperationException($"v{version}:typescript/src/index.ts declares no REGISTRY_SNAPSHOT_SHA256");
            var environment = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? string.Empty, StringComparer.Ordinal);
            environment["QYL_EXPECTED_REGISTRY_SHA256"] = digest;

            ProcessTasks.StartProcess("bash", $"eng/release/verify-packages.sh {version} https://api.nuget.org/v3/index.json", RootDirectory, environment)
                .AssertZeroExitCode();
            ProcessTasks.StartProcess("bash", $"eng/release/verify-typescript.sh {version}", RootDirectory, environment)
                .AssertZeroExitCode();
        });

    [GeneratedRegex("REGISTRY_SNAPSHOT_SHA256 = \"([0-9a-f]{64})\"")]
    private static partial Regex RegistryDigest();

    /// <summary>
    ///   Regenerate the analyzer index, per-rule pages, migration catalog, SARIF, and
    ///   editorconfig profiles from <c>DiagnosticDescriptors</c> and
    ///   <c>SemconvMigrationCatalog</c>. Every QYL rule's <c>HelpLinkUri</c> resolves to its
    ///   generated per-rule page.
    /// </summary>
    Target GenerateDocs => _ => _
        .Description("Regenerate analyzer documentation and machine-readable rule artifacts.")
        .Executes(() => RunDocsGenerator(buildGenerator: true));

    /// <summary>Print catalog statistics (curated vs supplemental, fixable counts, ...). No file I/O.</summary>
    Target AuditDocs => _ => _
        .Description("Print analyzer catalog statistics.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator(buildGenerator: false, "--audit"));

    /// <summary>
    ///   Rewrite analyzer source files in-place to make class names, class XML doc summaries, and
    ///   DiagnosticId-const docs match the runtime <c>DiagnosticDescriptor.Id</c> they register.
    ///   Run after an intentional rule-id change; review the diff before committing.
    /// </summary>
    Target EnforceIdsApply => _ => _
        .Description("Rewrite analyzer files to align class/doc/id-const with runtime descriptors.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator(buildGenerator: false, "--enforce-ids", "--apply"));

    /// <summary>Each application argument reaches the generator as its own argv entry.</summary>
    void RunDocsGenerator(bool buildGenerator, params string[] applicationArguments)
    {
        var settings = new DotNetRunSettings()
            .SetProjectFile(DocsGeneratorProject)
            .SetConfiguration("Release");

        settings = buildGenerator
            ? settings.SetProcessEnvironmentVariable("_QylInsideConsistencyCheck", "true")
            : settings.EnableNoBuild().EnableNoRestore();

        DotNetTasks.DotNetRun(settings.SetApplicationArguments(applicationArguments));
        Log.Information("DocsGenerator finished ({Mode}).",
            applicationArguments.Length == 0 ? "generate" : string.Join(' ', applicationArguments));
    }
}
