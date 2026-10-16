using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

[TestClass]
public sealed class AnalyzerRunTests(TestContext testContext)
{
    [TestMethod]
    public async Task An_analyzer_failure_cannot_be_filtered_into_an_empty_successful_result()
    {
        var fixture = Compile.Source(FailingAnalyzerSource)
            .WithReference<DiagnosticAnalyzer>().WithReference<ImmutableArray<DiagnosticDescriptor>>().Build();
        Assert.IsTrue(fixture.Succeeded, fixture.FormatDiagnostics());
        var analyzer = Assert.IsInstanceOfType<DiagnosticAnalyzer>(fixture.Assembly!.CreateInstance("FailingAnalyzer"));

        var failure = await Assert.ThrowsExactlyAsync<AssertFailedException>(() => AnalyzerRun.DiagnosticsAsync(
            analyzer, "QYL9998", "public sealed class Input { }",
            cancellationToken: testContext.CancellationToken));

        StringAssert.Contains(failure.Message, "AD0001");
        StringAssert.Contains(failure.Message, "controlled-analyzer-failure");
    }

    private const string FailingAnalyzerSource = """
        using System;
        using System.Collections.Immutable;
        using Microsoft.CodeAnalysis;
        using Microsoft.CodeAnalysis.Diagnostics;

        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        public sealed class FailingAnalyzer : DiagnosticAnalyzer
        {
            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
                [new("QYL9998", "Probe", "Probe", "Testing", DiagnosticSeverity.Warning, true)];

            public override void Initialize(AnalysisContext context)
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.EnableConcurrentExecution();
                context.RegisterCompilationAction(static _ => throw new InvalidOperationException("controlled-analyzer-failure"));
            }
        }
        """;
}
