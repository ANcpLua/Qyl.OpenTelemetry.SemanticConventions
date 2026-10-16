using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security;
using ANcpLua.Roslyn.Utilities.Testing;
using ANcpLua.Roslyn.Utilities.Testing.MSBuild;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
///   Tests for the narrowed QYL0304 trigger: it fires only when an AOT publish (PublishAot=true,
///   EventSourceSupport unset) actually contains in-process EventSource/EventListener instrumentation —
///   not on every AOT project.
/// </summary>
[TestClass]
public sealed class Qyl0304EnableEventSourceSupportAnalyzerTests(TestContext testContext)
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
    private static readonly Dictionary<string, string> AotNoSwitch = new()
    {
        ["build_property.PublishAot"] = "true"
    };
    private async Task<ImmutableArray<Diagnostic>> RunAsync(string source, IReadOnlyDictionary<string, string> buildProperties)
    {
        var diagnostics = await AnalyzerRun.CompileSource(Compile.Source(source)).WithAnalyzers([new Qyl0304EnableEventSourceSupportAnalyzer()], new AnalyzerOptions([], new DictionaryAnalyzerConfigOptionsProvider(buildProperties))).GetAnalyzerDiagnosticsAsync(testContext.CancellationToken);
        Assert.IsFalse(diagnostics.Any(static diagnostic => diagnostic.Id.StartsWith("AD", StringComparison.Ordinal)),
            "Analyzer execution failed:\n" + string.Join("\n", diagnostics));
        return[..diagnostics.Where(d => d.Id == "QYL0304")];
    }

    [TestMethod]
    public async Task Fires_when_aot_and_eventsource_subclass_present()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);
        Assert.ContainsSingle(diagnostics);
        Assert.Contains("MySource", diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [Timeout(120_000, CooperativeCancellation = true)]
    public async Task Packaged_build_transitive_props_expose_aot_properties_to_real_msbuild_consumers()
    {
        string repositoryRoot = RepositoryRoot.LocateFrom(AppContext.BaseDirectory, "Qyl.Telemetry.SemanticConventions.slnx");
        var analyzerProject = Path.Combine(repositoryRoot, "src", "Qyl.Telemetry.SemanticConventions.Analyzers", "Qyl.Telemetry.SemanticConventions.Analyzers.csproj");
        var temporaryDirectory = Directory.CreateTempSubdirectory("qyl0304-msbuild-");
        var cancellationToken = testContext.CancellationToken;
        try
        {
            var feedDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.FullName, "feed"));
            var pack = await RunDotnetAsync(repositoryRoot, cancellationToken, "pack", analyzerProject, "--configuration", "Release", "--no-build", "--no-restore", "--output", feedDirectory.FullName, $"-p:PackageVersion={AnalyzerTestVersion}", "--nologo");
            Assert.AreEqual(0, pack.ExitCode, string.Format(CultureInfo.InvariantCulture, "the analyzer package must pack successfully:\n{0}", pack.Output));
            var packagePath = Assert.ContainsSingle(Directory.EnumerateFiles(feedDirectory.FullName, "*.nupkg"));
            await using (var package = await ZipFile.OpenReadAsync(packagePath, cancellationToken))
            {
                var entries = package.Entries.Select(static entry => entry.FullName).ToArray();
                Assert.Contains($"analyzers/dotnet/cs/{AnalyzerPackageId}.dll", entries);
                Assert.Contains($"buildTransitive/{AnalyzerPackageId}.props", entries);
            }

            var nugetConfigPath = Path.Combine(temporaryDirectory.FullName, "NuGet.Config");
            await File.WriteAllTextAsync(nugetConfigPath, $$"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="qyl-test" value="{{SecurityElement.Escape(feedDirectory.FullName)}}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """, cancellationToken);
            var disabled = await BuildPackageConsumerAsync(repositoryRoot, temporaryDirectory.FullName, nugetConfigPath, eventSourceSupport: false, cancellationToken);
            Assert.Contains("build_property.PublishAot = true", disabled.EditorConfig);
            Assert.Contains("build_property.EventSourceSupport = false", disabled.EditorConfig);
            Assert.AreNotEqual(0, disabled.ExitCode, string.Format(CultureInfo.InvariantCulture, "QYL0304 is promoted to an error when EventSourceSupport is disabled:\n{0}", disabled.Output));
            Assert.Contains("error QYL0304", disabled.Output);
            var enabled = await BuildPackageConsumerAsync(repositoryRoot, temporaryDirectory.FullName, nugetConfigPath, eventSourceSupport: true, cancellationToken);
            Assert.Contains("build_property.PublishAot = true", enabled.EditorConfig);
            Assert.Contains("build_property.EventSourceSupport = true", enabled.EditorConfig);
            Assert.AreEqual(0, enabled.ExitCode, string.Format(CultureInfo.InvariantCulture, "EventSourceSupport=true suppresses QYL0304 in the packaged analyzer:\n{0}", enabled.Output));
            Assert.DoesNotContain("QYL0304", enabled.Output);
        }
        finally
        {
            temporaryDirectory.Delete(recursive: true);
        }
    }

    [TestMethod]
    public async Task Fires_when_aot_and_eventlistener_subclass_present()
    {
        var diagnostics = await RunAsync(EventListenerSubclass, AotNoSwitch);
        Assert.ContainsSingle(diagnostics);
        Assert.Contains("MyListener", diagnostics[0].GetMessage(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public async Task Silent_on_plain_aot_app_without_eventsource()
    {
        // The whole point of the narrowing: a pure OTLP-push AOT app is not nagged.
        var diagnostics = await RunAsync(PlainClass, AotNoSwitch);
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Silent_when_eventsourcesupport_enabled()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, new Dictionary<string, string> { ["build_property.PublishAot"] = "true", ["build_property.EventSourceSupport"] = "true" });
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Silent_when_not_publishing_aot()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, new Dictionary<string, string>());
        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Report_location_points_at_the_type()
    {
        var diagnostics = await RunAsync(EventSourceSubclass, AotNoSwitch);
        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual("QYL0304", diagnostic.Id);
        Assert.IsTrue(diagnostic.Location.IsInSource);
        Assert.AreEqual("MySource", EventSourceSubclass.Substring(diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length));
    }

    private static async Task<ConsumerBuildResult> BuildPackageConsumerAsync(string repositoryRoot, string temporaryRoot, string nugetConfigPath, bool eventSourceSupport, CancellationToken cancellationToken)
    {
        var caseName = eventSourceSupport ? "eventsource-enabled" : "eventsource-disabled";
        var consumerDirectory = Directory.CreateDirectory(Path.Combine(temporaryRoot, caseName));
        var projectPath = Path.Combine(consumerDirectory.FullName, "Consumer.csproj");
        var packagesPath = Path.Combine(temporaryRoot, "packages");
        await File.WriteAllTextAsync(projectPath, $$"""
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
            """, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(consumerDirectory.FullName, "Program.cs"), EventSourceApp, cancellationToken);
        var restore = await RunDotnetAsync(repositoryRoot, cancellationToken, "restore", projectPath, "--configfile", nugetConfigPath, "--packages", packagesPath, "--no-cache", "--nologo");
        Assert.AreEqual(0, restore.ExitCode, string.Format(CultureInfo.InvariantCulture, "the packaged analyzer consumer must restore:\n{0}", restore.Output));
        var build = await RunDotnetAsync(repositoryRoot, cancellationToken, "build", projectPath, "--configuration", "Release", "--no-restore", "--nologo", "--verbosity", "minimal");
        var editorConfigPath = Assert.ContainsSingle(Directory.EnumerateFiles(consumerDirectory.FullName, "*.GeneratedMSBuildEditorConfig.editorconfig", SearchOption.AllDirectories));
        var editorConfig = await File.ReadAllTextAsync(editorConfigPath, cancellationToken);
        return new ConsumerBuildResult(build.ExitCode, build.Output, editorConfig);
    }

    private static async Task<ProcessResult> RunDotnetAsync(string workingDirectory, CancellationToken cancellationToken, params string[] arguments)
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
        Assert.IsNotNull(process);
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, (await standardOutput) + (await standardError));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output);
    private sealed record ConsumerBuildResult(int ExitCode, string Output, string EditorConfig);
}
