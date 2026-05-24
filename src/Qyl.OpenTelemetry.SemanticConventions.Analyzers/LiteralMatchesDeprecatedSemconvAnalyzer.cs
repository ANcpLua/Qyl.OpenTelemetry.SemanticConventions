// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0012: Flags telemetry attribute key literals whose values match a
/// semantic-convention attribute name that is marked <c>[Obsolete]</c> in the
/// consumer's resolved <c>OpenTelemetry.SemanticConventions</c> assembly.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DeprecatedSemconvAnalyzer"/> (QYL0010), which fires
/// on direct typed-constant references. This rule catches the case where the
/// consumer hardcodes a literal, bypassing the typed constant entirely — a common
/// pattern in legacy code that the OTel SDK's <c>SetTag(string, …)</c> overloads encourage.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LiteralMatchesDeprecatedSemconvAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.LiteralMatchesDeprecatedSemconv];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var deprecationMap = BuildDeprecationMap(context.Compilation);
        if (deprecationMap.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, deprecationMap),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, deprecationMap),
            OperationKind.ObjectCreation);
        context.RegisterOperationAction(
            ctx => AnalyzeCollectionExpression(ctx, deprecationMap),
            OperationKind.CollectionExpression);
        context.RegisterOperationAction(
            ctx => AnalyzeAssignment(ctx, deprecationMap),
            OperationKind.SimpleAssignment);
    }

    private static Dictionary<string, string> BuildDeprecationMap(Compilation compilation)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        Walk(compilation.GlobalNamespace, map);
        return map;
    }

    private static void Walk(INamespaceSymbol ns, Dictionary<string, string> map)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol { IsConst: true } field
                    || field.Type.SpecialType != SpecialType.System_String
                    || field.DeclaredAccessibility != Accessibility.Public
                    || field.ConstantValue is not string value
                    || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                var obsolete = field.GetAttributes().FirstOrDefault(IsObsoleteAttribute);
                if (obsolete is null)
                {
                    continue;
                }

                var message = ExtractObsoleteMessage(obsolete);
                if (!map.ContainsKey(value))
                {
                    map[value] = message;
                }
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            Walk(nested, map);
        }
    }

    private static bool IsObsoleteAttribute(AttributeData attribute)
    {
        var attrClass = attribute.AttributeClass;
        return attrClass is { Name: "ObsoleteAttribute", ContainingNamespace.Name: "System" };
    }

    private static string ExtractObsoleteMessage(AttributeData obsolete)
    {
        if (obsolete.ConstructorArguments.Length > 0
            && obsolete.ConstructorArguments[0].Value is string message
            && !string.IsNullOrEmpty(message))
        {
            return message;
        }
        return "no replacement message provided";
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        Dictionary<string, string> deprecationMap)
    {
        var invocation = (IInvocationOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeInvocation(
            invocation,
            payload => ReportIfDeprecated(context, deprecationMap, payload));
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        Dictionary<string, string> deprecationMap)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeObjectCreation(
            objectCreation,
            payload => ReportIfDeprecated(context, deprecationMap, payload));
    }

    private static void AnalyzeCollectionExpression(
        OperationAnalysisContext context,
        Dictionary<string, string> deprecationMap)
    {
        var collectionExpression = (ICollectionExpressionOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeCollectionExpression(
            collectionExpression,
            payload => ReportIfDeprecated(context, deprecationMap, payload));
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        Dictionary<string, string> deprecationMap)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeAssignment(
            assignment,
            payload => ReportIfDeprecated(context, deprecationMap, payload));
    }

    private static void ReportIfDeprecated(
        OperationAnalysisContext context,
        Dictionary<string, string> deprecationMap,
        TelemetryAttributePayloadLiteral payload)
    {
        // Only fire on bare literals; QYL0010 already handles typed-constant references.
        if (!payload.KeyIsBareLiteral
            || !deprecationMap.TryGetValue(payload.Key, out var deprecationMessage))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty;
        if (SemconvCodeFixHelpers.TryExtractExactReplacement(deprecationMessage, out var replacement))
        {
            properties = properties.Add(SemconvCodeFixHelpers.ReplacementValueProperty, replacement);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.LiteralMatchesDeprecatedSemconv,
            payload.KeySyntax.GetLocation(),
            properties,
            payload.Key,
            deprecationMessage));
    }
}
