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
///         Two paths reach a metric. The source-generator path is a <c>[Tag]</c> parameter on
///         a <c>[Counter]</c> or <c>[Histogram]</c> method. The SDK path is a literal key in
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
    private enum KnownType { TagAttribute, CounterAttribute, HistogramAttribute }

    private static readonly string[] s_knownTypeNames = [
        "Qyl.Instrumentation.Instrumentation.TagAttribute",
        "Qyl.Instrumentation.Instrumentation.CounterAttribute",
        "Qyl.Instrumentation.Instrumentation.HistogramAttribute"
    ];

    /// <summary>
    ///     Known high-cardinality tag patterns, matched on segment boundaries so
    ///     <c>user.id</c>, <c>user_id</c>, <c>userId</c> and <c>app.user.id</c> all match
    ///     and <c>enduser.id</c> does not.
    /// </summary>
    private static readonly AttributeKeyPatternSet s_highCardinalityPatterns = new(
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

    private static readonly DiagnosticDescriptor s_highCardinalityMetricTagRule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Metrics,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_highCardinalityMetricTagRule];

    /// <summary>Registers compilation start action to resolve metric attribute types once.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context) {
        // The SDK path needs no Qyl.Instrumentation reference, so it registers unconditionally.
        TelemetryAttributePayloadDetection.RegisterPayloadAnalysis(context, ReportIfHighCardinalityMetricTag);

        var cache = new TypeCache<KnownType>(type => context.Compilation.GetTypeByMetadataName(s_knownTypeNames[(int)type]));

        if (cache.Get(KnownType.TagAttribute) is null) {
            return;
        }

        if (cache.Get(KnownType.CounterAttribute) is null && cache.Get(KnownType.HistogramAttribute) is null) {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeParameterForHighCardinalityTags(ctx, cache),
            SyntaxKind.Parameter);
    }

    private static void ReportIfHighCardinalityMetricTag(OperationAnalysisContext context, TelemetryAttributePayloadLiteral payload) {
        if (payload.Sink != TelemetryPayloadSink.MetricMeasurement || !s_highCardinalityPatterns.Matches(payload.Key)) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            s_highCardinalityMetricTagRule,
            payload.KeySyntax.GetLocation(),
            payload.Key));
    }

    private static void AnalyzeParameterForHighCardinalityTags(SyntaxNodeAnalysisContext context, TypeCache<KnownType> cache) {
        var parameter = (ParameterSyntax)context.Node;

        if (parameter.AttributeLists.Count is 0) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(parameter, context.CancellationToken) is not { ContainingSymbol: IMethodSymbol methodSymbol } parameterSymbol
            || (!cache.HasAttribute(methodSymbol, KnownType.CounterAttribute) && !cache.HasAttribute(methodSymbol, KnownType.HistogramAttribute))
            || cache.GetAttribute(parameterSymbol, KnownType.TagAttribute) is not { ConstructorArguments: [{ Value: string tagName }, ..] }) {
            return;
        }

        if (s_highCardinalityPatterns.Matches(tagName)) {
            context.ReportDiagnostic(Diagnostic.Create(
                s_highCardinalityMetricTagRule,
                parameter.GetLocation(),
                tagName));
        }
    }
}
