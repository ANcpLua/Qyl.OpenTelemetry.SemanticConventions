using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>QYL0110: a catch that rethrows inside a starter's scope records the failure through RecordFailure (ADR D4).</summary>
public sealed class Qyl0110MissingRecordFailureAnalyzerTests
{
    [Fact]
    public async Task Reports_a_rethrowing_catch_in_a_starter_scope_without_RecordFailure()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0110MissingRecordFailureAnalyzer(),
            "QYL0110",
            """
            using System;
            using Qyl;

            public static class Checkout
            {
                public static void Run(Action pay)
                {
                    using var span = Spans.Work("checkout");
                    try { pay(); }
                    catch (InvalidOperationException) { throw; }
                }

                public static void RunScoped(Action pay)
                {
                    using (var span = Spans.Work("checkout"))
                    {
                        try { pay(); }
                        catch (Exception ex) { throw new InvalidOperationException("payment failed", ex); }
                    }
                }
            }
            """);

        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(static d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Is_silent_with_RecordFailure_without_a_rethrow_or_outside_a_starter_scope()
    {
        var diagnostics = await AnalyzerRun.DiagnosticsAsync(
            new Qyl0110MissingRecordFailureAnalyzer(),
            "QYL0110",
            """
            using System;
            using Qyl;

            public static class Checkout
            {
                public static void Recorded(Action pay)
                {
                    using var span = Spans.Work("checkout");
                    try { pay(); }
                    catch (Exception ex) { span?.RecordFailure(ex); throw; }
                }

                public static void Handled(Action pay)
                {
                    using var span = Spans.Work("checkout");
                    try { pay(); }
                    catch (InvalidOperationException) { }
                }

                public static void NoStarter(Action pay)
                {
                    try { pay(); }
                    catch (Exception) { throw; }
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
