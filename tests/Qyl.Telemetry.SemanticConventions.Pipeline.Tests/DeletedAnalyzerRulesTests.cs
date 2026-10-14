using ANcpLua.Roslyn.Utilities.Testing.MSBuild;
using AwesomeAssertions;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// The rules ADR D1 deletes (the [Traced]/[AgentTraced]/[Meter] family over types no repository
/// ships, the service-defaults checks, QYL0303, QYL0100 and QYL0101) are gone from the analyzer,
/// its release tracking and its documentation.
/// </summary>
public sealed class DeletedAnalyzerRulesTests
{
    private static readonly string[] Deleted =
    [
        "QYL0100", "QYL0101", "QYL0102", "QYL0103", "QYL0104", "QYL0105", "QYL0106", "QYL0107", "QYL0108", "QYL0109",
        "QYL0300", "QYL0302", "QYL0303", "QYL0405", "QYL0500", "QYL0501",
    ];

    [Fact]
    public void No_analyzer_reports_a_deleted_rule()
    {
        var ids = typeof(Qyl0200TelemetryNameAnalyzer).Assembly.GetTypes()
            .Where(static type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .SelectMany(static type => ((DiagnosticAnalyzer)Activator.CreateInstance(type)!).SupportedDiagnostics)
            .Select(static descriptor => descriptor.Id)
            .ToHashSet(StringComparer.Ordinal);

        ids.Should().NotIntersectWith(Deleted);
        ids.Should().Contain(["QYL0110", "QYL0111", "QYL0112", "QYL0113", "QYL0120", "QYL0121", "QYL0122"]);
    }

    [Fact]
    public void The_release_tracking_and_the_rule_pages_name_no_deleted_rule()
    {
        var root = RepositoryRoot.LocateFrom(AppContext.BaseDirectory, "Qyl.Telemetry.SemanticConventions.slnx");
        var analyzers = Path.Combine(root, "src", "Qyl.Telemetry.SemanticConventions.Analyzers");
        var releases = File.ReadAllText(Path.Combine(analyzers, "AnalyzerReleases.Shipped.md"))
                       + File.ReadAllText(Path.Combine(analyzers, "AnalyzerReleases.Unshipped.md"));
        var pages = Directory.GetFiles(Path.Combine(root, "docs", "rules")).Select(Path.GetFileName).ToList();

        foreach (var id in Deleted)
        {
            releases.Should().NotContain(id);
            pages.Should().NotContain(page => page!.StartsWith(id + "_", StringComparison.Ordinal));
        }
    }
}
