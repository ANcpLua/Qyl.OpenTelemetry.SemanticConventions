#if NETSTANDARD2_0
namespace System.Diagnostics.CodeAnalysis;

/// <summary>Supplies nullable-flow metadata absent from the netstandard2.0 reference assembly.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
[JetBrains.Annotations.UsedImplicitly]
internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
{
    public bool ReturnValue { get; } = returnValue;
}
#endif
