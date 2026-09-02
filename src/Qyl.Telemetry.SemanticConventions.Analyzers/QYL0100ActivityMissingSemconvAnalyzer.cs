
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0100: Detects Activity/Span creation without semantic convention attributes.
/// </summary>
/// <remarks>
///     <para>
///         OpenTelemetry Activities (Spans) should include semantic convention attributes
///         appropriate for their operation type to enable correlation, filtering, and analysis.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0100ActivityMissingSemconvAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0100.</summary>
    private const string DiagnosticId = "QYL0100";

    // Operation types and their expected semantic convention prefixes
    private static readonly Dictionary<string, string[]> s_operationTypePrefixes = new(StringComparer.OrdinalIgnoreCase) {
        ["http"] = ["http.", "url.", "server.", "client.", "network.", "user_agent."],
        ["db"] = ["db."],
        ["rpc"] = ["rpc.", "jsonrpc."],
        ["messaging"] = ["messaging."],
        ["faas"] = ["faas."],
        ["gen_ai"] = ["gen_ai.", "openai."]
    };

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.Suggestion);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers operation actions to analyze Activity.StartActivity calls.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context) {
        var invocation = (IInvocationOperation)context.Operation;

        if (invocation.TargetMethod.Name != "StartActivity" ||
            !invocation.TryGetConstantArgument<string>(0, out var activityName) ||
            InferOperationType(activityName) is not { } operationType ||
            !s_operationTypePrefixes.TryGetValue(operationType, out var expectedPrefixes)) {
            return;
        }

        var setTags = CollectSetTagCalls(invocation);
        if (!HasRelevantSemconv(setTags, expectedPrefixes)) {
            context.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.Syntax.GetLocation(), activityName, operationType));
        }
    }

    private static bool HasRelevantSemconv(HashSet<string> tagNames, string[] expectedPrefixes) {
        foreach (var tagName in tagNames) {
            if (expectedPrefixes.Any(tagName.StartsWithIgnoreCase)) {
                return true;
            }

            if (OpenTelemetryDeprecatedSemconvCatalog.TryGetDeprecatedAttribute(tagName, out var deprecatedAttribute) &&
                expectedPrefixes.Any(deprecatedAttribute.Replacement.StartsWithIgnoreCase)) {
                return true;
            }

            if (OpenTelemetryDeprecatedSemconvCatalog.TryGetDeprecatedGenAiAttribute(tagName, out var genAiReplacement) &&
                expectedPrefixes.Any(genAiReplacement.StartsWithIgnoreCase)) {
                return true;
            }
        }

        return false;
    }

    private static string? InferOperationType(string activityName) {
        foreach (var kvp in s_operationTypePrefixes) {
            if (activityName.ContainsIgnoreCase(kvp.Key)) {
                return kvp.Key;
            }
        }

        // Additional heuristics
        if (activityName.ContainsIgnoreCase("request") ||
            activityName.ContainsIgnoreCase("response") ||
            activityName.ContainsIgnoreCase("get") ||
            activityName.ContainsIgnoreCase("post")) {
            return "http";
        }

        if (activityName.ContainsIgnoreCase("query") ||
            activityName.ContainsIgnoreCase("execute") ||
            activityName.ContainsIgnoreCase("select") ||
            activityName.ContainsIgnoreCase("insert")) {
            return "db";
        }

        if (activityName.ContainsIgnoreCase("chat") ||
            activityName.ContainsIgnoreCase("completion") ||
            activityName.ContainsIgnoreCase("embedding") ||
            activityName.ContainsIgnoreCase("llm")) {
            return "gen_ai";
        }

        return null;
    }

    private static HashSet<string> CollectSetTagCalls(IInvocationOperation startActivity) {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (startActivity.GetContainingBlock() is { } block) {
            var calls = new List<TagSetterCall>();
            TagSetterDetection.CollectTagSetterCalls(block, calls);
            foreach (var call in calls) {
                tags.Add(call.Key);
            }
        }

        return tags;
    }
}
