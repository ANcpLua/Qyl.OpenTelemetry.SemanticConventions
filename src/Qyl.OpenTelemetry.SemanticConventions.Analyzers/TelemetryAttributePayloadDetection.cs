// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

internal static class TelemetryAttributePayloadDetection
{
    private static readonly ImmutableHashSet<string> BaggageMethodNames = ImmutableHashSet.Create(
        "SetBaggage",
        "AddBaggage");

    public static void AnalyzeInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (IsKnownTelemetryKeyValueInvocation(invocation))
        {
            AnalyzeKeyValueInvocation(invocation, report);
        }

        if (IsMetricMeasurementInvocation(invocation))
        {
            AnalyzeArgumentsAfterFirst(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, report);
        }

        if (IsActivitySourceStartActivity(invocation)
            && TryGetArgumentByNameOrOrdinal(invocation.Arguments, "tags", 3, out var startActivityTagsArgument))
        {
            AnalyzePayload(startActivityTagsArgument.Value, report);
            return;
        }

        if (IsLoggerBeginScope(invocation)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var scopeStateArgument))
        {
            AnalyzePayload(scopeStateArgument.Value, report);
            return;
        }

        if (IsLoggerLog(invocation)
            && TryGetArgumentByNameOrOrdinal(invocation.Arguments, "state", 2, out var logStateArgument))
        {
            AnalyzePayload(logStateArgument.Value, report);
            return;
        }

        if (IsResourceBuilderAddAttributes(invocation)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var attributesArgument))
        {
            AnalyzePayload(attributesArgument.Value, report);
            return;
        }

        if (!IsInsideKnownTelemetryAttributePayload(invocation)
            && invocation.TargetMethod.Name == "Add"
            && IsStringKeyDictionary(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType))
        {
            AnalyzeAddLikeInvocation(invocation, report);
        }
    }

    public static void AnalyzeObjectCreation(
        IObjectCreationOperation objectCreation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (IsKeyValuePairStringObject(objectCreation.Type)
            && !IsInsideKnownTelemetryAttributePayload(objectCreation))
        {
            AnalyzeKeyValuePairCreation(objectCreation, report);
        }

        if (objectCreation.Initializer is not null
            && IsStringKeyDictionary(objectCreation.Type)
            && IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(objectCreation))
        {
            AnalyzeObjectInitializer(objectCreation.Initializer, report);
            return;
        }

        if (IsMetricMeasurementCreation(objectCreation.Type))
        {
            AnalyzeArgumentsAfterFirst(objectCreation.Arguments, extensionMethod: false, report);
        }

        if (IsActivityEventCreation(objectCreation.Type)
            && TryGetArgumentByNameOrOrdinal(objectCreation.Arguments, "tags", 2, out var tagsArgument))
        {
            AnalyzePayload(tagsArgument.Value, report);
        }

        if (IsActivityLinkCreation(objectCreation.Type)
            && TryGetArgumentByNameOrOrdinal(objectCreation.Arguments, "tags", 1, out var linkTagsArgument))
        {
            AnalyzePayload(linkTagsArgument.Value, report);
        }
    }

    public static void AnalyzeCollectionExpression(
        ICollectionExpressionOperation collectionExpression,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(collectionExpression))
        {
            AnalyzeCollectionExpressionElements(collectionExpression, report);
        }
    }

    public static void AnalyzeAssignment(
        ISimpleAssignmentOperation assignment,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (IsTelemetryTagCollectionIndexerAssignment(assignment)
            || IsDictionaryIndexerAssignmentOnLocalFlowingToTelemetry(assignment))
        {
            AnalyzeIndexerAssignment(assignment, report);
        }
    }

    private static void AnalyzePayload(
        IOperation operation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);

        if (unwrapped is IArrayCreationOperation arrayCreation)
        {
            if (arrayCreation.Initializer is not null)
            {
                AnalyzeArrayInitializer(arrayCreation.Initializer, report);
            }

            return;
        }

        if (unwrapped is IArrayInitializerOperation arrayInitializer)
        {
            AnalyzeArrayInitializer(arrayInitializer, report);
            return;
        }

        if (unwrapped is ICollectionExpressionOperation collectionExpression)
        {
            AnalyzeCollectionExpressionElements(collectionExpression, report);
            return;
        }

        if (unwrapped is IObjectCreationOperation objectCreation)
        {
            if (IsKeyValuePairStringObject(objectCreation.Type))
            {
                AnalyzeKeyValuePairCreation(objectCreation, report);
            }

            if (objectCreation.Initializer is not null)
            {
                AnalyzeObjectInitializer(objectCreation.Initializer, report);
            }

            return;
        }

        if (unwrapped is IInvocationOperation { TargetMethod.Name: "Add" } invocation)
        {
            AnalyzeAddLikeInvocation(invocation, report);
            return;
        }

        if (unwrapped is ISimpleAssignmentOperation assignment)
        {
            AnalyzeIndexerAssignment(assignment, report);
        }
    }

    private static void AnalyzeKeyValueInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        string? value = null;
        SyntaxNode? valueSyntax = null;
        if (TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument))
        {
            TryGetValue(valueArgument.Value, out value, out valueSyntax);
        }

        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeArrayInitializer(
        IArrayInitializerOperation initializer,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        foreach (var element in initializer.ElementValues)
        {
            AnalyzePayload(element, report);
        }
    }

    private static void AnalyzeObjectInitializer(
        IObjectOrCollectionInitializerOperation initializer,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        foreach (var initializerOperation in initializer.Initializers)
        {
            AnalyzePayload(initializerOperation, report);
        }
    }

    private static void AnalyzeCollectionExpressionElements(
        ICollectionExpressionOperation collectionExpression,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        foreach (var element in collectionExpression.Elements)
        {
            if (element is ISpreadOperation spread)
            {
                AnalyzePayload(spread.Operand, report);
                continue;
            }

            AnalyzePayload(element, report);
        }
    }

    private static void AnalyzeKeyValuePairCreation(
        IObjectCreationOperation objectCreation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var keyArgument)
            || !TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 1, out var valueArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        TryGetValue(valueArgument.Value, out var value, out var valueSyntax);
        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeAddLikeInvocation(
        IInvocationOperation invocation,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetKey(keyArgument.Value, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        string? value = null;
        SyntaxNode? valueSyntax = null;
        if (TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument))
        {
            TryGetValue(valueArgument.Value, out value, out valueSyntax);
        }

        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeIndexerAssignment(
        ISimpleAssignmentOperation assignment,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        if (!TryGetIndexerKey(assignment.Target, out var key, out var keySyntax, out var keyIsBareLiteral))
        {
            return;
        }

        TryGetValue(assignment.Value, out var value, out var valueSyntax);
        report(new TelemetryAttributePayloadLiteral(key, keySyntax, keyIsBareLiteral, value, valueSyntax));
    }

    private static void AnalyzeArgumentsAfterFirst(
        ImmutableArray<IArgumentOperation> arguments,
        bool extensionMethod,
        Action<TelemetryAttributePayloadLiteral> report)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            var logicalOrdinal = argument.Parameter is null
                ? i
                : argument.Parameter.Ordinal - (extensionMethod ? 1 : 0);
            if (logicalOrdinal <= 0 && i == 0)
            {
                continue;
            }

            AnalyzePayload(argument.Value, report);
        }
    }

    private static bool TryGetKey(
        IOperation operation,
        [NotNullWhen(true)] out string? key,
        [NotNullWhen(true)] out SyntaxNode? syntax,
        out bool isBareLiteral)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        isBareLiteral = unwrapped.Syntax is LiteralExpressionSyntax;
        if (TagSetterDetection.TryGetNonEmptyStringConstant(unwrapped, out key))
        {
            syntax = unwrapped.Syntax;
            return true;
        }

        syntax = null;
        return false;
    }

    private static bool TryGetValue(
        IOperation operation,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(true)] out SyntaxNode? syntax)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (TagSetterDetection.TryGetStringConstant(unwrapped, out value))
        {
            syntax = unwrapped.Syntax;
            return true;
        }

        syntax = null;
        return false;
    }

    private static bool TryGetIndexerKey(
        IOperation operation,
        [NotNullWhen(true)] out string? key,
        [NotNullWhen(true)] out SyntaxNode? syntax,
        out bool isBareLiteral)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (unwrapped is IPropertyReferenceOperation propertyReference)
        {
            foreach (var argument in propertyReference.Arguments)
            {
                if (IsLogicalArgument(argument, extensionMethod: false, 0)
                    && TryGetKey(argument.Value, out key, out syntax, out isBareLiteral))
                {
                    return true;
                }
            }
        }

        key = null;
        syntax = null;
        isBareLiteral = false;
        return false;
    }

    private static bool TryGetArgumentByOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        bool extensionMethod,
        int logicalParameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        foreach (var candidate in arguments)
        {
            if (candidate.Parameter?.Ordinal == parameterOrdinal)
            {
                argument = candidate;
                return true;
            }
        }

        var fallbackIndex = arguments.Length > parameterOrdinal ? parameterOrdinal : logicalParameterOrdinal;
        if (arguments.Length > fallbackIndex)
        {
            argument = arguments[fallbackIndex];
            return true;
        }

        argument = null;
        return false;
    }

    private static bool TryGetArgumentByNameOrOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        string parameterName,
        int parameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        foreach (var candidate in arguments)
        {
            if (string.Equals(candidate.Parameter?.Name, parameterName, StringComparison.Ordinal))
            {
                argument = candidate;
                return true;
            }
        }

        return TryGetArgumentByOrdinal(arguments, extensionMethod: false, parameterOrdinal, out argument);
    }

    private static bool IsLogicalArgument(
        IArgumentOperation argument,
        bool extensionMethod,
        int logicalParameterOrdinal)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        return argument.Parameter?.Ordinal == parameterOrdinal;
    }

    private static bool IsAfterFirstLogicalArgument(
        IArgumentOperation argument,
        bool extensionMethod)
    {
        if (argument.Parameter is null)
        {
            return false;
        }

        return argument.Parameter.Ordinal - (extensionMethod ? 1 : 0) > 0;
    }

    private static bool IsResourceBuilderAddAttributes(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "AddAttributes")
        {
            return false;
        }

        if (invocation.TargetMethod.ContainingType.Name == "ResourceBuilder")
        {
            return true;
        }

        return invocation.TargetMethod.IsExtensionMethod
            && invocation.TargetMethod.Parameters.Length > 0
            && invocation.TargetMethod.Parameters[0].Type.Name == "ResourceBuilder";
    }

    private static bool IsKnownTelemetryKeyValueInvocation(IInvocationOperation invocation)
    {
        if (TagSetterDetection.IsTagSetterInvocation(invocation)
            || BaggageMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return true;
        }

        return invocation.TargetMethod.Name == "Add"
            && IsTelemetryTagCollection(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType);
    }

    private static bool IsMetricMeasurementInvocation(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name is "Add" or "Record"
        && IsMetricInstrument(invocation.TargetMethod.ContainingType);

    private static bool IsInsideKnownTelemetryAttributePayload(IOperation operation)
    {
        if (IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(operation))
        {
            return true;
        }

        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is not IArgumentOperation argument)
            {
                continue;
            }

            if (argument.Parent is IInvocationOperation invocation
                && IsResourceBuilderAddAttributes(invocation)
                && IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation metricInvocation
                && IsMetricMeasurementInvocation(metricInvocation)
                && IsAfterFirstLogicalArgument(argument, metricInvocation.TargetMethod.IsExtensionMethod))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation metricMeasurement
                && IsMetricMeasurementCreation(metricMeasurement.Type)
                && IsAfterFirstLogicalArgument(argument, extensionMethod: false))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation startActivityInvocation
                && IsActivitySourceTagsArgument(startActivityInvocation, argument))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation loggerInvocation
                && IsLoggerPayloadArgument(loggerInvocation, argument))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation objectCreation
                && (IsActivityEventTagsArgument(objectCreation, argument)
                    || IsActivityLinkTagsArgument(objectCreation, argument)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(IOperation operation)
    {
        if (!TryGetEnclosingLocalInitializer(operation, out var local))
        {
            return false;
        }

        return LocalFlowsToKnownTelemetryAttributePayload(local, operation);
    }

    private static bool TryGetEnclosingLocalInitializer(
        IOperation operation,
        [NotNullWhen(true)] out ILocalSymbol? local)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IVariableInitializerOperation
                && current.Parent is IVariableDeclaratorOperation { Symbol: ILocalSymbol localSymbol })
            {
                local = localSymbol;
                return true;
            }
        }

        local = null;
        return false;
    }

    private static bool LocalFlowsToKnownTelemetryAttributePayload(
        ILocalSymbol local,
        IOperation operation)
    {
        var root = operation;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        foreach (var descendant in Microsoft.CodeAnalysis.Operations.OperationExtensions.DescendantsAndSelf(root))
        {
            if (descendant is not IArgumentOperation argument
                || !IsLocalReference(argument.Value, local))
            {
                continue;
            }

            if (argument.Parent is IInvocationOperation invocation
                && ((IsResourceBuilderAddAttributes(invocation)
                        && IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0))
                    || (IsMetricMeasurementInvocation(invocation)
                        && IsAfterFirstLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod))
                    || IsActivitySourceTagsArgument(invocation, argument)
                    || IsLoggerPayloadArgument(invocation, argument)))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation objectCreation
                && (IsActivityEventTagsArgument(objectCreation, argument)
                    || IsActivityLinkTagsArgument(objectCreation, argument)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDictionaryIndexerAssignmentOnLocalFlowingToTelemetry(ISimpleAssignmentOperation assignment)
    {
        var target = TagSetterDetection.UnwrapConversion(assignment.Target);
        return target is IPropertyReferenceOperation propertyReference
            && IsStringKeyDictionary(propertyReference.Instance?.Type ?? propertyReference.Property.ContainingType)
            && TryGetLocalReference(propertyReference.Instance, out var local)
            && LocalFlowsToKnownTelemetryAttributePayload(local, assignment);
    }

    private static bool IsTelemetryTagCollectionIndexerAssignment(ISimpleAssignmentOperation assignment)
    {
        var target = TagSetterDetection.UnwrapConversion(assignment.Target);
        return target is IPropertyReferenceOperation propertyReference
            && IsTelemetryTagCollection(propertyReference.Instance?.Type ?? propertyReference.Property.ContainingType);
    }

    private static bool TryGetLocalReference(
        IOperation? operation,
        [NotNullWhen(true)] out ILocalSymbol? local)
    {
        if (operation is not null
            && TagSetterDetection.UnwrapConversion(operation) is ILocalReferenceOperation localReference)
        {
            local = localReference.Local;
            return true;
        }

        local = null;
        return false;
    }

    private static bool IsLocalReference(IOperation operation, ILocalSymbol local) =>
        TagSetterDetection.UnwrapConversion(operation) is ILocalReferenceOperation localReference
        && SymbolEqualityComparer.Default.Equals(localReference.Local, local);

    private static bool IsActivityEventTagsArgument(
        IObjectCreationOperation objectCreation,
        IArgumentOperation argument) =>
        IsActivityEventCreation(objectCreation.Type)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 2);

    private static bool IsActivityLinkTagsArgument(
        IObjectCreationOperation objectCreation,
        IArgumentOperation argument) =>
        IsActivityLinkCreation(objectCreation.Type)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 1);

    private static bool IsActivitySourceTagsArgument(
        IInvocationOperation invocation,
        IArgumentOperation argument) =>
        IsActivitySourceStartActivity(invocation)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 3);

    private static bool IsLoggerPayloadArgument(
        IInvocationOperation invocation,
        IArgumentOperation argument)
    {
        if (IsLoggerBeginScope(invocation))
        {
            return IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0);
        }

        return IsLoggerLog(invocation)
            && (string.Equals(argument.Parameter?.Name, "state", StringComparison.Ordinal)
                || IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 2));
    }

    private static bool IsKeyValuePairStringObject(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } named)
        {
            return false;
        }

        return named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsStringKeyDictionary(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length < 2)
        {
            return false;
        }

        return named.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary"
            && named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsTelemetryTagCollection(ITypeSymbol? type) =>
        type?.Name is "TagList" or "ActivityTagsCollection";

    private static bool IsActivityEventCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityEvent";

    private static bool IsActivityLinkCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityLink";

    private static bool IsActivitySourceStartActivity(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "StartActivity"
        && invocation.TargetMethod.ContainingType.Name == "ActivitySource";

    private static bool IsLoggerBeginScope(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "BeginScope"
        && IsLoggerLike(invocation.TargetMethod.ContainingType);

    private static bool IsLoggerLog(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "Log"
        && IsLoggerLike(invocation.TargetMethod.ContainingType);

    private static bool IsMetricInstrument(ITypeSymbol? type) =>
        type?.Name is "Counter" or "Histogram" or "UpDownCounter";

    private static bool IsMetricMeasurementCreation(ITypeSymbol? type) =>
        type?.Name is "Measurement";

    private static bool IsLoggerLike(ITypeSymbol? type) =>
        type?.Name is "ILogger";
}

internal readonly struct TelemetryAttributePayloadLiteral
{
    public TelemetryAttributePayloadLiteral(
        string key,
        SyntaxNode keySyntax,
        bool keyIsBareLiteral,
        string? value,
        SyntaxNode? valueSyntax)
    {
        Key = key;
        KeySyntax = keySyntax;
        KeyIsBareLiteral = keyIsBareLiteral;
        Value = value;
        ValueSyntax = valueSyntax;
    }

    public string Key { get; }

    public SyntaxNode KeySyntax { get; }

    public bool KeyIsBareLiteral { get; }

    public string? Value { get; }

    public SyntaxNode? ValueSyntax { get; }
}
