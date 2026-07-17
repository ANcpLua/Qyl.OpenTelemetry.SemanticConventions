namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0013: Detects telemetry attributes whose statically known value type contradicts
/// the type in the complete pinned semantic-convention registry.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Qyl0013IncorrectAttributeTypeAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0013";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverity.Warning);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!TagSetterDetection.IsTagSetterInvocation(invocation)
            || !TagSetterDetection.TryGetTagSetterKeyArgument(invocation, out var keyArgument)
            || !TagSetterDetection.TryGetTagSetterValueArgument(invocation, out var valueArgument)
            || !TagSetterDetection.TryGetNonEmptyStringConstant(keyArgument.Value, out var attributeName)
            || !SemconvRegistryFacts.TryGetAttributeType(attributeName, out var expectedKind)
            || expectedKind is SemconvAttributeValueKind.Any or SemconvAttributeValueKind.Unknown)
        {
            return;
        }

        var value = valueArgument.Value.UnwrapAllConversions();
        if (value.Type is null || IsTypeMatch(value.Type, expectedKind))
        {
            return;
        }

        context.ReportDiagnostic(
            s_rule,
            valueArgument.Syntax.GetLocation(),
            attributeName,
            GetTypeName(expectedKind),
            value.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
    }

    private static bool IsTypeMatch(ITypeSymbol actualType, SemconvAttributeValueKind expectedKind) =>
        expectedKind switch
        {
            SemconvAttributeValueKind.String => actualType.IsString(),
            SemconvAttributeValueKind.Integer => IsIntegerType(actualType),
            SemconvAttributeValueKind.Double => IsFloatingPointType(actualType),
            SemconvAttributeValueKind.Boolean => actualType.IsBoolean(),
            SemconvAttributeValueKind.StringArray => HasSequenceElementType(actualType, TypeSymbolExtensions.IsString),
            SemconvAttributeValueKind.IntegerArray => HasSequenceElementType(actualType, IsIntegerType),
            SemconvAttributeValueKind.DoubleArray => HasSequenceElementType(actualType, IsFloatingPointType),
            SemconvAttributeValueKind.BooleanArray => HasSequenceElementType(actualType, TypeSymbolExtensions.IsBoolean),
            _ => true,
        };

    private static bool IsIntegerType(ITypeSymbol type) =>
        type.SpecialType is
            SpecialType.System_SByte or
            SpecialType.System_Byte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64;

    private static bool IsFloatingPointType(ITypeSymbol type) =>
        type.SpecialType is
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal;

    private static bool HasSequenceElementType(ITypeSymbol type, Func<ITypeSymbol, bool> predicate)
    {
        if (type is IArrayTypeSymbol array)
        {
            return predicate(array.ElementType);
        }

        if (type is not INamedTypeSymbol named)
        {
            return false;
        }

        if (TryGetEnumerableElement(named, out var directElement) && predicate(directElement))
        {
            return true;
        }

        foreach (var contract in named.AllInterfaces)
        {
            if (TryGetEnumerableElement(contract, out var element) && predicate(element))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetEnumerableElement(
        INamedTypeSymbol type,
        [NotNullWhen(true)] out ITypeSymbol? element)
    {
        if (type is
            {
                Name: "IEnumerable",
                Arity: 1,
                ContainingNamespace.Name: "Generic",
                ContainingNamespace.ContainingNamespace.Name: "Collections",
                ContainingNamespace.ContainingNamespace.ContainingNamespace.Name: "System",
            })
        {
            element = type.TypeArguments[0];
            return true;
        }

        element = null;
        return false;
    }

    private static string GetTypeName(SemconvAttributeValueKind expectedKind) =>
        expectedKind switch
        {
            SemconvAttributeValueKind.String => "string",
            SemconvAttributeValueKind.Integer => "integer",
            SemconvAttributeValueKind.Double => "double",
            SemconvAttributeValueKind.Boolean => "boolean",
            SemconvAttributeValueKind.StringArray => "string[]",
            SemconvAttributeValueKind.IntegerArray => "integer[]",
            SemconvAttributeValueKind.DoubleArray => "double[]",
            SemconvAttributeValueKind.BooleanArray => "boolean[]",
            _ => "unknown",
        };
}
