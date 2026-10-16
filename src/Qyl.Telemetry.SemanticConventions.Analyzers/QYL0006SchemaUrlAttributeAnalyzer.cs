namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>Reports a schema URL stored as a resource attribute instead of resource schema metadata.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0006SchemaUrlAttributeAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0006";

    private static readonly DiagnosticDescriptor Rule = CreateRule(
        DiagnosticId, DiagnosticCategories.OpenTelemetry, DiagnosticSeverity.Info);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
            TelemetryAttributePayloadDetection.RegisterPayloadAnalysis(start, (operation, payload) =>
            {
                if (payload is { Sink: TelemetryPayloadSink.ResourceAttributes, Key: "telemetry.schema_url" })
                {
                    operation.ReportDiagnostic(Diagnostic.Create(Rule, payload.KeySyntax.GetLocation()));
                }
            }));
}
