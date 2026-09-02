using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl;
using Qyl.Telemetry.SemanticConventions.Names;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

public sealed class AgentDiagnosticVocabularyTests
{
    [Fact]
    public void Agent_diagnostic_snapshot_uses_one_fixed_event_and_fixed_summary_keys()
    {
        QylTelemetryNames.Events.QylAgentDiagnosticSnapshot.Should()
            .Be("qyl.agent.diagnostic.snapshot");

        (string Actual, string Expected)[] summaryKeys =
        [
            (QylAttributes.AgentDiagnosticExtensionId, "qyl.agent.diagnostic.extension.id"),
            (QylAttributes.AgentDiagnosticFormatVersion, "qyl.agent.diagnostic.format.version"),
            (QylAttributes.AgentDiagnosticSnapshotId, "qyl.agent.diagnostic.snapshot.id"),
            (QylAttributes.AgentDiagnosticProbeId, "qyl.agent.diagnostic.probe.id"),
            (QylAttributes.AgentDiagnosticPhase, "qyl.agent.diagnostic.phase"),
            (QylAttributes.AgentDiagnosticOutcome, "qyl.agent.diagnostic.outcome"),
            (QylAttributes.AgentDiagnosticVariableCount, "qyl.agent.diagnostic.variable.count"),
            (QylAttributes.AgentDiagnosticCheckCount, "qyl.agent.diagnostic.check.count"),
            (QylAttributes.AgentDiagnosticCheckFailedCount, "qyl.agent.diagnostic.check.failed_count"),
        ];

        summaryKeys.Select(static item => item.Actual).Should().OnlyHaveUniqueItems();
        summaryKeys.Should().AllSatisfy(static item => item.Actual.Should().Be(item.Expected));
    }

    [Fact]
    public void Agent_diagnostic_phase_and_outcome_values_are_machine_tokens()
    {
        string[] phases =
        [
            QylAttributes.AgentDiagnosticPhaseValues.Input,
            QylAttributes.AgentDiagnosticPhaseValues.Output,
            QylAttributes.AgentDiagnosticPhaseValues.Error,
            QylAttributes.AgentDiagnosticPhaseValues.Checkpoint,
        ];
        string[] outcomes =
        [
            QylAttributes.AgentDiagnosticOutcomeValues.Pass,
            QylAttributes.AgentDiagnosticOutcomeValues.Fail,
            QylAttributes.AgentDiagnosticOutcomeValues.Unknown,
            QylAttributes.AgentDiagnosticOutcomeValues.NotEvaluated,
        ];

        phases.Should().Equal("input", "output", "error", "checkpoint");
        outcomes.Should().Equal("pass", "fail", "unknown", "not_evaluated");
    }

    [Fact]
    public void Workflow_correlation_uses_fixed_qyl_keys()
    {
        (string Actual, string Expected)[] correlationKeys =
        [
            (QylAttributes.WorkflowRunId, "qyl.workflow.run.id"),
            (QylAttributes.WorkflowEventId, "qyl.workflow.event.id"),
            (QylAttributes.WorkflowAttemptId, "qyl.workflow.attempt.id"),
            (QylAttributes.WorkflowAgentId, "qyl.workflow.agent.id"),
            (QylAttributes.WorkflowToolCallId, "qyl.workflow.tool_call.id"),
        ];

        correlationKeys.Select(static item => item.Actual).Should().OnlyHaveUniqueItems();
        correlationKeys.Should().AllSatisfy(static item => item.Actual.Should().Be(item.Expected));
    }
}
