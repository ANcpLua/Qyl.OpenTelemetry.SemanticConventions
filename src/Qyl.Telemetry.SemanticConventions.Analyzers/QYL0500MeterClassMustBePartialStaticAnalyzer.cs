
namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0500: Detects [Meter] classes that are not declared as partial static.
/// </summary>
/// <remarks>
///     <para>
///         The source generator requires [Meter] classes to be partial static because:
///         <list type="bullet">
///             <item>The generator creates static Meter and instrument fields</item>
///             <item>The generator implements partial methods that record metrics</item>
///             <item>Static classes ensure single instance of meter/instruments</item>
///         </list>
///     </para>
///     <para>
///         Example of correct usage:
///         <code>
///         [Meter("MyApp")]
///         public static partial class AppMetrics
///         {
///             [Counter("orders.created")]
///             public static partial void RecordOrderCreated([Tag("status")] string status);
///         }
///         </code>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0500MeterClassMustBePartialStaticAnalyzer : AlAnalyzer {
    /// <summary>The diagnostic identifier for QYL0500.</summary>
    public const string DiagnosticId = "QYL0500";

    private const string MeterAttributeFullName = "Qyl.Instrumentation.Instrumentation.MeterAttribute";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.Metrics,
        DiagnosticSeverities.RequiredFix);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Registers syntax node actions to analyze class declarations with [Meter] attribute.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context) {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (classDeclaration.AttributeLists.Count is 0) {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken) is not { } classSymbol
            || !classSymbol.HasAttribute(MeterAttributeFullName)) {
            return;
        }

        if (!classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || !classDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword)) {
            context.ReportDiagnostic(Diagnostic.Create(
                s_rule,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name));
        }
    }
}
