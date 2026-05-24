// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using MsOperationExtensions = Microsoft.CodeAnalysis.Operations.OperationExtensions;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Shared detection helpers for tag-setter callsites across the analyzers.
/// </summary>
internal static class TagSetterDetection
{
    /// <summary>
    /// Method names treated as a "set this telemetry tag" callsite. Matched by simple
    /// name against <see cref="IInvocationOperation.TargetMethod"/>; we do not pin the
    /// receiver type because consumers use Activity, Span, TagList, custom builders,
    /// and OTel-extension wrappers interchangeably.
    /// </summary>
    public static readonly ImmutableHashSet<string> TagSetterMethodNames = ImmutableHashSet.Create(
        "SetTag", "AddTag", "SetAttribute", "AddAttribute");

    /// <summary>
    /// Unwraps surrounding implicit conversions (e.g. <c>string</c> → <c>object?</c>
    /// when calling <c>SetTag(string, object?)</c>) to expose the underlying operand
    /// whose <c>ConstantValue</c> we want to inspect.
    /// </summary>
    public static IOperation UnwrapConversion(IOperation operation)
    {
        return operation.UnwrapImplicitConversions();
    }

    public static bool IsTagSetterInvocation(IInvocationOperation invocation)
    {
        return TagSetterMethodNames.Contains(invocation.TargetMethod.Name);
    }

    public static bool TryGetTagSetterKeyArgument(
        IInvocationOperation invocation,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        return TryGetTagSetterArgument(invocation, logicalParameterOrdinal: 0, out argument);
    }

    public static bool TryGetTagSetterValueArgument(
        IInvocationOperation invocation,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        return TryGetTagSetterArgument(invocation, logicalParameterOrdinal: 1, out argument);
    }

    public static bool TryGetNonEmptyStringConstant(IOperation operation, [NotNullWhen(true)] out string? value)
    {
        if (!TryGetStringConstant(operation, out value) || string.IsNullOrEmpty(value))
        {
            value = null;
            return false;
        }

        return true;
    }

    public static bool TryGetStringConstant(IOperation operation, [NotNullWhen(true)] out string? value)
    {
        if (operation.UnwrapImplicitConversions().TryGetConstantValue<string>(out var constantValue)
            && constantValue is not null)
        {
            value = constantValue;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Walks an operation tree and yields every tag-setter invocation whose first
    /// argument is a constant string. Captures the constant key plus optional
    /// constant value (for callers that need to match on the value too).
    /// </summary>
    public static void CollectTagSetterCalls(
        IOperation root,
        List<TagSetterCall> sink)
    {
        foreach (var descendant in MsOperationExtensions.DescendantsAndSelf(root))
        {
            if (descendant is not IInvocationOperation invocation)
            {
                continue;
            }

            if (!IsTagSetterInvocation(invocation)
                || !TryGetTagSetterKeyArgument(invocation, out var keyArgument)
                || !TryGetNonEmptyStringConstant(keyArgument.Value, out var key))
            {
                continue;
            }

            string? value = null;
            if (TryGetTagSetterValueArgument(invocation, out var valueArgument)
                && TryGetStringConstant(valueArgument.Value, out var constantValue))
            {
                value = constantValue;
            }

            sink.Add(new TagSetterCall(key, value, keyArgument));
        }
    }

    private static bool TryGetTagSetterArgument(
        IInvocationOperation invocation,
        int logicalParameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        // Extension methods expose the receiver as parameter 0. Prefer Roslyn's
        // parameter binding so named/reordered arguments still resolve correctly.
        var parameterOrdinal = invocation.TargetMethod.IsExtensionMethod
            ? logicalParameterOrdinal + 1
            : logicalParameterOrdinal;

        foreach (var candidate in invocation.Arguments)
        {
            if (candidate.Parameter?.Ordinal == parameterOrdinal)
            {
                argument = candidate;
                return true;
            }
        }

        var fallbackIndex = invocation.Arguments.Length > parameterOrdinal
            ? parameterOrdinal
            : logicalParameterOrdinal;

        if (invocation.Arguments.Length > fallbackIndex)
        {
            argument = invocation.Arguments[fallbackIndex];
            return true;
        }

        argument = null;
        return false;
    }
}

/// <summary>Captured tag-setter callsite with constant key and optional constant value.</summary>
internal readonly struct TagSetterCall
{
    public TagSetterCall(string key, string? value, IArgumentOperation keyArgument)
    {
        Key = key;
        Value = value;
        KeyArgument = keyArgument;
    }

    public string Key { get; }
    public string? Value { get; }

    public IArgumentOperation KeyArgument { get; }

    /// <summary>The syntax location of the key argument (suitable for diagnostic reports).</summary>
    public Location KeyLocation => KeyArgument.Syntax.GetLocation();
}
