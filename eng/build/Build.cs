// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Serilog;

namespace Qyl.Telemetry.SemanticConventions.Build;

/// <summary>
///   Repository-local build host: compile, and the analyzer documentation targets. The
///   compiled packages' constant classes are generated at build by the repository's own
///   source generator, so there is no checked-in constant tree to guard.
/// </summary>
internal sealed class Build : NukeBuild
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
        .Executes(() => RunDocsGenerator(applicationArguments: null, buildGenerator: true));

    /// <summary>Print catalog statistics (curated vs supplemental, fixable counts, ...). No file I/O.</summary>
    Target AuditDocs => _ => _
        .Description("Print analyzer catalog statistics.")
        .DependsOn(Compile)
        .Executes(() => RunDocsGenerator("--audit"));

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
}
