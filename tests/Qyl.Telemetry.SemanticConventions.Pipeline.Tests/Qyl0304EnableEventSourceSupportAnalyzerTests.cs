using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
///   Tests for the narrowed QYL0304 trigger: it fires only when an AOT publish (PublishAot=true,
///   EventSourceSupport unset) actually contains in-process EventSource/EventListener instrumentation —
///   not on every AOT project.
/// </summary>
public sealed class Qyl0304EnableEventSourceSupportAnalyzerTests
{
    private const string EventSourceSubclass = """
        using System.Diagnostics.Tracing;
        public sealed class MySource : EventSource { }
        """;

    private const string EventListenerSubclass = """
        using System.Diagnostics.Tracing;
        public sealed class MyListener : EventListener { }
        """;

    private const string PlainClass = "public class C { }";

    private const string AnalyzerPackageId = "Qyl.Telemetry.SemanticConventions.Analyzers";

    private const string AnalyzerTestVersion = "0.0.0-qyl0304-test";

    private const string EventSourceApp = """
        using System.Diagnostics.Tracing;
        internal sealed class SmokeEventSource : EventSource { }
        internal static class Program { public static void Main() { } }
        """;

    private static readonly Dictionary<string, string> AotNoSwitch =
        new() { ["build_property.PublishAot"] = "true" };

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        IReadOnlyDictionary<string, string> buildProperties)
    {
        var diagnostics = await AnalyzerHarness.RunAsync(
            [new Qyl0304EnableEventSourceSupportAnalyzer()],
            source,
            options: new AnalyzerOptions([], new GlobalOptionsProvider(buildProperties)));
        return [.. diagnostics.Where(d => d.Id == "QYL0304")];
    }

    [Fact]
    public async Task Fires_when_aot_and_eventsource_subclass_present()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("MySource");
    }

    [Fact]
    public async Task Packaged_build_transitive_props_expose_aot_properties_to_real_msbuild_consumers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var analyzerProject = Path.Combine(
            repositoryRoot,
            "src",
            "Qyl.Telemetry.SemanticConventions.Analyzers",
            "Qyl.Telemetry.SemanticConventions.Analyzers.csproj");
        var temporaryDirectory = Directory.CreateTempSubdirectory("qyl0304-msbuild-");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            var feedDirectory = Directory.CreateDirectory(
                Path.Combine(temporaryDirectory.FullName, "feed"));
            var pack = await RunDotnetAsync(
                repositoryRoot,
                cancellationToken,
                "pack",
                analyzerProject,
                "--configuration",
                "Release",
                "--no-build",
                "--no-restore",
                "--output",
                feedDirectory.FullName,
                $"-p:PackageVersion={AnalyzerTestVersion}",
                "--nologo");
            pack.ExitCode.Should().Be(0, "the analyzer package must pack successfully:\n{0}", pack.Output);

            var packagePath = Directory.EnumerateFiles(feedDirectory.FullName, "*.nupkg").Should()
                .ContainSingle().Which;
            using (var package = ZipFile.OpenRead(packagePath))
            {
                var entries = package.Entries.Select(static entry => entry.FullName);
                entries.Should().Contain($"analyzers/dotnet/cs/{AnalyzerPackageId}.dll");
                entries.Should().Contain($"buildTransitive/{AnalyzerPackageId}.props");
            }

            var nugetConfigPath = Path.Combine(temporaryDirectory.FullName, "NuGet.Config");
            await File.WriteAllTextAsync(
                nugetConfigPath,
                $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="qyl-test" value="{{SecurityElement.Escape(feedDirectory.FullName)}}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """,
                cancellationToken);

            var disabled = await BuildPackageConsumerAsync(
                repositoryRoot,
                temporaryDirectory.FullName,
                nugetConfigPath,
                eventSourceSupport: false,
                cancellationToken);
            disabled.EditorConfig.Should().Contain("build_property.PublishAot = true");
            disabled.EditorConfig.Should().Contain("build_property.EventSourceSupport = false");
            disabled.ExitCode.Should().NotBe(0,
                "QYL0304 is promoted to an error when EventSourceSupport is disabled:\n{0}", disabled.Output);
            disabled.Output.Should().Contain("error QYL0304");

            var enabled = await BuildPackageConsumerAsync(
                repositoryRoot,
                temporaryDirectory.FullName,
                nugetConfigPath,
                eventSourceSupport: true,
                cancellationToken);
            enabled.EditorConfig.Should().Contain("build_property.PublishAot = true");
            enabled.EditorConfig.Should().Contain("build_property.EventSourceSupport = true");
            enabled.ExitCode.Should().Be(0,
                "EventSourceSupport=true suppresses QYL0304 in the packaged analyzer:\n{0}", enabled.Output);
            enabled.Output.Should().NotContain("QYL0304");
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Fires_when_aot_and_eventlistener_subclass_present()
    {
        var diagnostics = await RunAsync(EventListenerSubclass, AotNoSwitch);

        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Contain("MyListener");
    }

    [Fact]
    public async Task Silent_on_plain_aot_app_without_eventsource()
    {
        // The whole point of the narrowing: a pure OTLP-push AOT app is not nagged.
        var diagnostics = await RunAsync(PlainClass, AotNoSwitch);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_when_eventsourcesupport_enabled()
    {
        var diagnostics = await RunAsync(
            EventSourceSubclass,
            new Dictionary<string, string>
            {
                ["build_property.PublishAot"] = "true",
                ["build_property.EventSourceSupport"] = "true"
            });

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Silent_when_not_publishing_aot()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, new Dictionary<string, string>());

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Report_location_points_at_the_type()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);

        diagnostics[0].Location.IsInSource.Should().BeTrue();
    }

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> global)
        : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options = new(global);
        public override AnalyzerConfigOptions GlobalOptions => _options;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;

        private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                if (values.TryGetValue(key, out var v))
                {
                    value = v;
                    return true;
                }

                value = null!;
                return false;
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Qyl.Telemetry.SemanticConventions.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the semantic-conventions repository root.");
    }

    private static async Task<ConsumerBuildResult> BuildPackageConsumerAsync(
        string repositoryRoot,
        string temporaryRoot,
        string nugetConfigPath,
        bool eventSourceSupport,
        CancellationToken cancellationToken)
    {
        var caseName = eventSourceSupport ? "eventsource-enabled" : "eventsource-disabled";
        var consumerDirectory = Directory.CreateDirectory(Path.Combine(temporaryRoot, caseName));
        var projectPath = Path.Combine(consumerDirectory.FullName, "Consumer.csproj");
        var packagesPath = Path.Combine(temporaryRoot, "packages");

        await File.WriteAllTextAsync(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <PublishAot>true</PublishAot>
                <EventSourceSupport>{{eventSourceSupport.ToString().ToLowerInvariant()}}</EventSourceSupport>
                <WarningsAsErrors>QYL0304</WarningsAsErrors>
                <EnableNETAnalyzers>false</EnableNETAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{{AnalyzerPackageId}}"
                                  Version="{{AnalyzerTestVersion}}"
                                  PrivateAssets="all" />
              </ItemGroup>
            </Project>
            """,
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory.FullName, "Program.cs"),
            EventSourceApp,
            cancellationToken);

        var restore = await RunDotnetAsync(
            repositoryRoot,
            cancellationToken,
            "restore",
            projectPath,
            "--configfile",
            nugetConfigPath,
            "--packages",
            packagesPath,
            "--no-cache",
            "--nologo");
        restore.ExitCode.Should().Be(0, "the packaged analyzer consumer must restore:\n{0}", restore.Output);

        var build = await RunDotnetAsync(
            repositoryRoot,
            cancellationToken,
            "build",
            projectPath,
            "--configuration",
            "Release",
            "--no-restore",
            "--nologo",
            "--verbosity",
            "minimal");
        var editorConfigPath = Directory.EnumerateFiles(
                consumerDirectory.FullName,
                "*.GeneratedMSBuildEditorConfig.editorconfig",
                SearchOption.AllDirectories)
            .Should().ContainSingle().Which;
        var editorConfig = await File.ReadAllTextAsync(editorConfigPath, cancellationToken);
        return new ConsumerBuildResult(build.ExitCode, build.Output, editorConfig);
    }

    private static async Task<ProcessResult> RunDotnetAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        process.Should().NotBeNull();
        var standardOutput = process!.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessResult(process.ExitCode, (await standardOutput) + (await standardError));
    }

    private sealed record ProcessResult(int ExitCode, string Output);

    private sealed record ConsumerBuildResult(int ExitCode, string Output, string EditorConfig);
}
