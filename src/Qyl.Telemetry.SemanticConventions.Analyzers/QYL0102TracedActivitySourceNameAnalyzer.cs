
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0102: Validates [Traced] attribute has non-empty ActivitySourceName.
/// </summary>
/// <remarks>
///     <para>
///         The [Traced] attribute requires a valid ActivitySourceName because:
///         <list type="bullet">
///             <item>The source name identifies where spans originate</item>
///             <item>It must match a registered ActivitySource in the tracing pipeline</item>
///             <item>Empty names prevent proper span correlation and filtering</item>
///         </list>
///     </para>
///     <para>
///         Example of correct usage:
///         <code>
///         [Traced("MyApp.Orders")]  // Valid: descriptive source name
///         public class OrderService { }
///
///         [Traced("")]  // Error: empty source name
///         public class BadService { }
///         </code>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0102TracedActivitySourceNameAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0102.</summary>
    public const string DiagnosticId = "QYL0102";

    private const string TracedAttributeFullName = "Qyl.Instrumentation.Instrumentation.TracedAttribute";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.RequiredFix);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers symbol actions to analyze types and methods with [Traced] attribute.</summary>
    protected override void InitializeCore(AnalysisContext context) {
        context.RegisterSymbolAction(
            static context => AnalyzeSymbol(context, context.Symbol),
            SymbolKind.NamedType,
            SymbolKind.Method);
    }

    private static void AnalyzeSymbol(SymbolAnalysisContext context, ISymbol symbol) {
        if (context.Compilation.GetTypeByMetadataName(TracedAttributeFullName) is not { } tracedAttributeType) {
            return;
        }

        foreach (var attribute in symbol.GetAttributes()) {
            if (!attribute.AttributeClass.IsEqualTo(tracedAttributeType)) {
                continue;
            }

            string? activitySourceName = null;

            if (attribute.ConstructorArguments is [{ Value: string ctorArg }, ..]) {
                activitySourceName = ctorArg;
            }

            foreach (var namedArg in attribute.NamedArguments) {
                if (namedArg is { Key: "ActivitySourceName", Value.Value: string namedValue }) {
                    activitySourceName = namedValue;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(activitySourceName)) {
                var location = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                               ?? Location.None;

                context.ReportDiagnostic(Diagnostic.Create(s_rule, location, symbol.Name));
            }
        }
    }
}
