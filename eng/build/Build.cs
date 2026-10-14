// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Solutions;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.DotNet;
using Serilog;

namespace Qyl.Telemetry.SemanticConventions.Build;

/// <summary>
///   Repository-local build host: compile, and the analyzer documentation targets. The
///   packages' generated surface is committed and guarded by scripts/check-generated.sh, so
///   nothing here regenerates it.
/// </summary>
internal sealed class Build : FalloutBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Solution(GenerateProjects = false)]
    readonly Solution Solution = null!;

    AbsolutePath DocsGeneratorProject =>
        RootDirectory / "tools"
        / "Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator"
        / "Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator.csproj";

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
