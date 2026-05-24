// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0011: When a string literal used as a telemetry attribute key matches a
/// known <c>const string</c> field exposed by
/// <c>OpenTelemetry.SemanticConventions.Attributes.*</c>, suggest the typed constant.
/// </summary>
/// <remarks>
/// The catalog is built at compilation-start by walking the consumer's referenced
/// SemConv assembly. If the consumer doesn't reference
/// <c>OpenTelemetry.SemanticConventions</c>, the analyzer is a silent no-op.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferSemconvConstantAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Property-bag key carrying the fully-qualified suggested constant for code-fix providers.</summary>
    public const string SuggestedConstantKey = "SuggestedConstant";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.PreferSemconvConstant];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var catalog = BuildCatalog(context.Compilation);
        if (catalog.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, catalog),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, catalog),
            OperationKind.ObjectCreation);
        context.RegisterOperationAction(
            ctx => AnalyzeCollectionExpression(ctx, catalog),
            OperationKind.CollectionExpression);
        context.RegisterOperationAction(
            ctx => AnalyzeAssignment(ctx, catalog),
            OperationKind.SimpleAssignment);
    }

    private static Dictionary<string, string> BuildCatalog(Compilation compilation)
    {
        var catalog = new Dictionary<string, string>(StringComparer.Ordinal);

        // Walk every assembly-defined namespace. Collect public const string fields
        // from any "*Attributes" type whose namespace is or descends from
        // OpenTelemetry.SemanticConventions. This works for upstream's flat layout
        // (OpenTelemetry.SemanticConventions.HttpAttributes), qyl's nested layout
        // (Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes),
        // and any other consumer that shares the SemanticConventions namespace root.
        WalkNamespace(compilation.GlobalNamespace, catalog);

        return catalog;
    }

    private static void WalkNamespace(INamespaceSymbol ns, Dictionary<string, string> catalog)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.Name.EndsWith("Attributes", StringComparison.Ordinal))
            {
                continue;
            }

            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol { IsConst: true } field
                    || field.Type.SpecialType != SpecialType.System_String
                    || field.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (field.ConstantValue is not string value || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                if (catalog.ContainsKey(value))
                {
                    continue;
                }

                catalog.Add(value, $"{type.Name}.{field.Name}");
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            WalkNamespace(nested, catalog);
        }
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog)
    {
        var invocation = (IInvocationOperation)context.Operation;

        TelemetryAttributePayloadDetection.AnalyzeInvocation(
            invocation,
            payload => ReportIfKnownConstant(context, catalog, payload));
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;

        TelemetryAttributePayloadDetection.AnalyzeObjectCreation(
            objectCreation,
            payload => ReportIfKnownConstant(context, catalog, payload));
    }

    private static void AnalyzeCollectionExpression(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog)
    {
        var collectionExpression = (ICollectionExpressionOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeCollectionExpression(
            collectionExpression,
            payload => ReportIfKnownConstant(context, catalog, payload));
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeAssignment(
            assignment,
            payload => ReportIfKnownConstant(context, catalog, payload));
    }

    private static void ReportIfKnownConstant(
        OperationAnalysisContext context,
        Dictionary<string, string> catalog,
        TelemetryAttributePayloadLiteral payload)
    {
        // Only flag bare string literals. Symbol references (typed SemConv constants,
        // user-defined locals/consts, members) are out of scope: if the user already
        // wrote a named symbol, even one whose value matches the catalog, we should
        // not nag; the symbol may be intentional or carry meaning beyond the value.
        if (!payload.KeyIsBareLiteral
            || !catalog.TryGetValue(payload.Key, out var suggestedConstant))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(SuggestedConstantKey, suggestedConstant);

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.PreferSemconvConstant,
            payload.KeySyntax.GetLocation(),
            properties,
            payload.Key,
            suggestedConstant));
    }
}
