namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0202: Warns about high-cardinality tags on metrics.
/// </summary>
/// <remarks>
///     <para>
///         High-cardinality tags (like user.id, request.id, session.id) create a
///         unique time series for each distinct value. This can cause:
///         <list type="bullet">
///             <item>Memory exhaustion in metrics backends (Prometheus, etc.)</item>
///             <item>Increased storage costs</item>
///             <item>Query performance degradation</item>
///             <item>Cardinality explosions that crash collectors</item>
///         </list>
///     </para>
///     <para>
///         A metric receives a literal key in
///         the tags of <c>Counter.Add</c>, <c>Histogram.Record</c>, <c>UpDownCounter.Add</c>
///         or a <c>Measurement</c>, including a <c>TagList</c> or dictionary built up in a
///         local and passed to one of those; that path is the shared
///         <see cref="TelemetryAttributePayloadDetection"/> filtered to
///         <see cref="TelemetryPayloadSink.MetricMeasurement"/>.
///     </para>
///     <para>
///         Alternatives to high-cardinality metric tags:
///         <list type="bullet">
///             <item>Use span/trace attributes instead (spans are sampled)</item>
///             <item>Aggregate into buckets (e.g., user_type instead of user_id)</item>
///             <item>Use exemplars to link metrics to traces</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0202HighCardinalityMetricTagAnalyzer : AlAnalyzer {
    /// <summary>
    ///     Known high-cardinality tag patterns, matched on segment boundaries so
    ///     <c>user.id</c>, <c>user_id</c>, <c>userId</c> and <c>app.user.id</c> all match
    ///     and <c>enduser.id</c> does not.
    /// </summary>
    private static readonly AttributeKeyPatternSet HighCardinalityPatterns = new(
        "user.id",
        "request.id",
        "session.id",
        "trace.id",
        "span.id",
        "correlation.id",
        "transaction.id",
        "message.id",
        "order.id",
        "customer.id",
        "account.id",
        "email",
        "ip",
        "user_agent",
        "url",
        "uri",
        "path",
        "query",
        "timestamp",
        "uuid",
        "guid");

    /// <summary>The diagnostic identifier for QYL0202.</summary>
    private const string DiagnosticId = "QYL0202";

    private static readonly DiagnosticDescriptor HighCardinalityMetricTagRule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Metrics,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [HighCardinalityMetricTagRule];

    /// <summary>Checks literal tag keys that reach .NET metric measurements.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(static start =>
            TelemetryAttributePayloadDetection.RegisterPayloadAnalysis(start, ReportIfHighCardinalityMetricTag));

    private static void ReportIfHighCardinalityMetricTag(OperationAnalysisContext context, TelemetryAttributePayloadLiteral payload) {
        if (payload.Sink != TelemetryPayloadSink.MetricMeasurement || !HighCardinalityPatterns.Matches(payload.Key)) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            HighCardinalityMetricTagRule,
            payload.KeySyntax.GetLocation(),
            payload.Key));
    }

}
