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
///         A schema URL is recognized in three forms: a string literal that names a schema
///         (<c>telemetry.schema_url</c>, <c>opentelemetry.io/schemas</c>, or any text
///         containing "schema"), a method whose name contains "Schema", or a constant-valued
///         expression whose name or resolved value does, such as the generated
///         <c>SchemaUrl.Current</c> or a consumer's own <c>const string</c>. The last form
///         goes through the semantic model, so a constant referenced by identifier counts the
///         same as the literal it stands for.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0006MissingSchemaUrlAnalyzer : AlAnalyzer {
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

        if (MentionsSchemaUrl(invocation, context.SemanticModel, context.CancellationToken)) {
            return;
        }

        var location = invocation.GetMethodLocation();
        context.ReportDiagnostic(s_rule, location);
    }

    private static bool MentionsSchemaUrl(SyntaxNode invocation, SemanticModel semanticModel, CancellationToken cancellationToken) {
        foreach (var node in invocation.DescendantNodes()) {
            switch (node) {
                case LiteralExpressionSyntax literal: {
                    if (MentionsSchema(literal.Token.ValueText)) {
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
                case IdentifierNameSyntax identifier: {
                    if (MentionsSchema(identifier.Identifier.ValueText)) {
                        return true;
                    }

                    break;
                }
                case MemberAccessExpressionSyntax memberAccess: {
                    // SchemaUrl.Current, or a consumer const whose value is the URL.
                    if (semanticModel.GetConstantValue(memberAccess, cancellationToken) is { HasValue: true, Value: string constant }
                        && MentionsSchema(constant)) {
                        return true;
                    }

                    break;
                }
            }
        }

        return false;
    }

    private static bool MentionsSchema(string text) =>
        text.ContainsIgnoreCase("schema");
}
