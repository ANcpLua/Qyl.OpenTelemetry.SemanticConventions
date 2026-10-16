namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0200: Reserves qyl-owned telemetry names for the generated registry catalog.
/// </summary>
/// <remarks>
///     <para>
///         The registry owns qyl's SDK vocabulary, not the application's vocabulary. Unknown names
///         outside the qyl namespace remain valid custom telemetry. Type, deprecation and value checks for known
///         semantic conventions are handled by their dedicated analyzers.
///     </para>
///     <para>
///         Name positions checked: <c>Activity.SetTag</c>/<c>AddTag</c> keys, <c>ActivityEvent</c> names,
///         <c>ActivitySource</c>/<c>ActivitySourceOptions</c> construction, <c>Meter</c>/<c>MeterOptions</c>
///         construction, and <c>Meter.Create*</c> instrument names. Arguments are resolved through constant
///         propagation, so both literals and <c>const string</c> references are checked. Non-constant
///         expressions (composed span names, dynamic values) are outside this rule. Generated code is skipped:
///         generated constants are trusted by construction because the generator derives them from the registry.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0200TelemetryNameAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0200.</summary>
    private const string DiagnosticId = "QYL0200";

    private const string ActivityTypeName = "System.Diagnostics.Activity";
    private const string ActivityEventTypeName = "System.Diagnostics.ActivityEvent";
    private const string ActivitySourceTypeName = "System.Diagnostics.ActivitySource";
    private const string ActivitySourceOptionsTypeName = "System.Diagnostics.ActivitySourceOptions";
    private const string MeterTypeName = "System.Diagnostics.Metrics.Meter";
    private const string MeterOptionsTypeName = "System.Diagnostics.Metrics.MeterOptions";

    private static readonly DiagnosticDescriptor Rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions for telemetry name positions.</summary>
    protected override void InitializeCore(AnalysisContext context) {
        context.RegisterOperationAction(
            static ctx => AnalyzeInvocation(ctx, (IInvocationOperation)ctx.Operation),
            OperationKind.Invocation);

        context.RegisterOperationAction(
            static ctx => AnalyzeObjectCreation(ctx, (IObjectCreationOperation)ctx.Operation),
            OperationKind.ObjectCreation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, IInvocationOperation invocation) {
        var containingType = invocation.TargetMethod.ContainingType?.ToDisplayString();
        switch (invocation.TargetMethod.Name) {
            case "SetTag" or "AddTag" when containingType is ActivityTypeName:
                CheckNamePosition(
                    context,
                    invocation.Arguments.FirstOrDefault()?.Value,
                    static value => SemconvRegistryFacts.IsKnownAttributeKey(value),
                    "attribute key");
                break;

            case "CreateCounter" or "CreateHistogram" or "CreateGauge" or "CreateUpDownCounter"
                or "CreateObservableCounter" or "CreateObservableGauge" or "CreateObservableUpDownCounter"
                when containingType is MeterTypeName:
                var nameArgument = invocation.Arguments
                    .FirstOrDefault(static argument => argument.Parameter?.Name is "name");
                CheckNamePosition(
                    context,
                    nameArgument?.Value,
                    static value => SemconvRegistryFacts.IsKnownMetricName(value),
                    "metric name");
                break;
        }
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context, IObjectCreationOperation creation) {
        var check = creation.Type?.ToDisplayString() switch {
            ActivitySourceTypeName or ActivitySourceOptionsTypeName or MeterTypeName or MeterOptionsTypeName =>
                (SemconvRegistryFacts.IsKnownScopeName, "instrumentation scope name"),
            ActivityEventTypeName =>
                (SemconvRegistryFacts.IsKnownEventName, "event name"),
            _ => default((Func<string, bool>?, string?)),
        };

        if (check.Item1 is null) {
            return;
        }

        CheckNamePosition(context, creation.Arguments.FirstOrDefault()?.Value, check.Item1, check.Item2!);
    }

    private static void CheckNamePosition(
        OperationAnalysisContext context,
        IOperation? argument,
        Func<string, bool> isKnown,
        string positionKind) {
        if (argument is null ||
            !argument.TryGetConstantValue<string>(out var name) ||
            string.IsNullOrEmpty(name) ||
            !IsReservedName(name) ||
            isKnown(name)) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, argument.Syntax.GetLocation(), name, positionKind));
    }

    private static bool IsReservedName(string name) =>
        name.StartsWith("qyl.", StringComparison.Ordinal) || name.StartsWith("Qyl.", StringComparison.Ordinal);
}
