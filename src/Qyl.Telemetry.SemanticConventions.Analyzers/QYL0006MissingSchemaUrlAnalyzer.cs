
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0006: Detects OpenTelemetry configurations that don't set the schema URL.
/// </summary>
/// <remarks>
///     <para>
///         The OpenTelemetry specification recommends setting a schema URL on resources
///         to indicate which version of the semantic conventions is being used. Without
///         a schema URL, telemetry backends cannot automatically transform attributes
///         between convention versions, potentially causing data inconsistencies.
///     </para>
///     <para>
///         The analyzer flags resource configuration calls like <c>ConfigureResource</c>,
///         <c>SetResourceBuilder</c>, and <c>AddResource</c> on OpenTelemetry builder
///         types when they do not include a schema URL reference.
///     </para>
///     <para>
///         Schema URL detection is heuristic: the analyzer looks for string literals
///         containing "schema", "telemetry.schema_url", or "opentelemetry.io/schemas",
///         as well as method calls with "Schema" in the name. This may not catch all
///         indirect configurations but covers common patterns.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0006MissingSchemaUrlAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0006.</summary>
    private const string DiagnosticId = "QYL0006";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverity.Info);

    /// <summary>Set of method names that configure OTel resources.</summary>
    private static readonly HashSet<string> s_resourceConfigMethods = [
        "ConfigureResource",
        "SetResourceBuilder",
        "AddResource",
        "WithResource",
        "ConfigureOpenTelemetry"
    ];

    /// <summary>Array of known OTel builder type names to check for resource configuration.</summary>
    private static readonly string[] s_otelBuilderTypeNames = [
        "OpenTelemetry.Trace.TracerProviderBuilder",
        "OpenTelemetry.Metrics.MeterProviderBuilder",
        "OpenTelemetry.Logs.LoggerProviderBuilder",
        "OpenTelemetry.OpenTelemetryBuilder",
        "OpenTelemetry.IOpenTelemetryBuilder"
    ];

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers compilation start action to analyze OTel resource configurations.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(OnCompilationStart);

    private static void OnCompilationStart(CompilationStartAnalysisContext context) {
        var otelBuilderTypes = s_otelBuilderTypeNames
            .Select(context.Compilation.GetTypeByMetadataName)
            .WhereNotNull()
            .ToImmutableArray();

        if (otelBuilderTypes.IsEmpty) {
            return;
        }

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeInvocation(ctx, otelBuilderTypes),
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> otelBuilderTypes) {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var methodName = invocation.GetMethodName();
        if (methodName is null || !s_resourceConfigMethods.Contains(methodName)) {
            return;
        }

        if (!BuilderCallDetection.IsBuilderCall(invocation, context.SemanticModel, otelBuilderTypes, context.CancellationToken)) {
            return;
        }

        if (CheckForSchemaUrl(invocation)) {
            return;
        }

        var location = invocation.GetMethodLocation();
        context.ReportDiagnostic(s_rule, location);
    }

    private static bool CheckForSchemaUrl(SyntaxNode invocation) {
        foreach (var node in invocation.DescendantNodes()) {
            switch (node) {
                case LiteralExpressionSyntax literal: {
                    var value = literal.Token.ValueText;
                    if (value.ContainsIgnoreCase("schema") ||
                        value.ContainsIgnoreCase("telemetry.schema_url") ||
                        value.ContainsIgnoreCase("opentelemetry.io/schemas")) {
                        return true;
                    }

                    break;
                }
                case InvocationExpressionSyntax nestedInvocation: {
                    var nestedMethod = nestedInvocation.GetMethodName();
                    if (nestedMethod?.ContainsIgnoreCase("Schema") == true) {
                        return true;
                    }

                    break;
                }
            }
        }

        return false;
    }
}
