// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0014: Flags telemetry attribute payloads where the value is a constant
/// string that matches a deprecated member of the corresponding
/// <c>*Values</c> nested class on the SemConv attribute type.
/// </summary>
/// <remarks>
/// Map is built from compiled symbols at compilation start. For each <c>*Attributes</c>
/// class, every nested <c>*Values</c> class is matched against its sibling
/// <c>Attribute*</c> constant by name (e.g. <c>HttpRequestMethodValues</c> ↔
/// <c>AttributeHttpRequestMethod</c>). <c>[Obsolete]</c>-marked value-class members
/// then become entries in a <c>(attrName, value) → message</c> map.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeprecatedSemconvValueAnalyzer : DiagnosticAnalyzer
{
    private const string ValuesSuffix = "Values";
    private const string AttributeConstPrefix = "Attribute";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.DeprecatedSemconvValue];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var map = BuildValueDeprecationMap(context.Compilation);
        if (map.Count == 0)
        {
            return;
        }

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, map),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, map),
            OperationKind.ObjectCreation);
        context.RegisterOperationAction(
            ctx => AnalyzeCollectionExpression(ctx, map),
            OperationKind.CollectionExpression);
        context.RegisterOperationAction(
            ctx => AnalyzeAssignment(ctx, map),
            OperationKind.SimpleAssignment);
    }

    private static Dictionary<(string AttrName, string Value), string> BuildValueDeprecationMap(Compilation compilation)
    {
        var map = new Dictionary<(string, string), string>();
        Walk(compilation.GlobalNamespace, map);
        return map;
    }

    private static void Walk(INamespaceSymbol ns, Dictionary<(string, string), string> map)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!SemconvNamespace.IsAttributesType(type))
            {
                continue;
            }

            // Build local map: AttributeXxx const name → its string value.
            Dictionary<string, string>? attrConstNameToValue = null;
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol { IsConst: true, Type.SpecialType: SpecialType.System_String, ConstantValue: string attrName } field
                    || string.IsNullOrEmpty(attrName))
                {
                    continue;
                }

                attrConstNameToValue ??= new Dictionary<string, string>(StringComparer.Ordinal);
                attrConstNameToValue[field.Name] = attrName;
            }

            if (attrConstNameToValue is null)
            {
                continue;
            }

            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.Name.EndsWith(ValuesSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = nested.Name.Substring(0, nested.Name.Length - ValuesSuffix.Length);
                var siblingConstName = AttributeConstPrefix + prefix;
                if (!attrConstNameToValue.TryGetValue(siblingConstName, out var attrName))
                {
                    continue;
                }

                foreach (var nestedMember in nested.GetMembers())
                {
                    if (nestedMember is not IFieldSymbol { IsConst: true } valueField
                        || valueField.Type.SpecialType != SpecialType.System_String
                        || valueField.ConstantValue is not string value)
                    {
                        continue;
                    }

                    var obsolete = valueField.GetAttributes().FirstOrDefault(IsObsoleteAttribute);
                    if (obsolete is null)
                    {
                        continue;
                    }

                    var message = ExtractObsoleteMessage(obsolete);
                    var key = (attrName, value);
                    if (!map.ContainsKey(key))
                    {
                        map[key] = message;
                    }
                }
            }
        }

        foreach (var nestedNs in ns.GetNamespaceMembers())
        {
            Walk(nestedNs, map);
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
        Dictionary<(string AttrName, string Value), string> map)
    {
        var invocation = (IInvocationOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeInvocation(
            invocation,
            payload => ReportIfDeprecated(context, map, payload));
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        Dictionary<(string AttrName, string Value), string> map)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeObjectCreation(
            objectCreation,
            payload => ReportIfDeprecated(context, map, payload));
    }

    private static void AnalyzeCollectionExpression(
        OperationAnalysisContext context,
        Dictionary<(string AttrName, string Value), string> map)
    {
        var collectionExpression = (ICollectionExpressionOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeCollectionExpression(
            collectionExpression,
            payload => ReportIfDeprecated(context, map, payload));
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        Dictionary<(string AttrName, string Value), string> map)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        TelemetryAttributePayloadDetection.AnalyzeAssignment(
            assignment,
            payload => ReportIfDeprecated(context, map, payload));
    }

    private static void ReportIfDeprecated(
        OperationAnalysisContext context,
        Dictionary<(string AttrName, string Value), string> map,
        TelemetryAttributePayloadLiteral payload)
    {
        if (payload.Value is null
            || payload.ValueSyntax is null
            || !map.TryGetValue((payload.Key, payload.Value), out var message))
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty;
        if (SemconvCodeFixHelpers.TryExtractExactReplacement(message, out var replacement))
        {
            properties = properties.Add(SemconvCodeFixHelpers.ReplacementValueProperty, replacement);
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DeprecatedSemconvValue,
            payload.ValueSyntax.GetLocation(),
            properties,
            payload.Value,
            payload.Key,
            message));
    }
}
