using System.Reflection;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Attributes.Http;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Every generated <c>…Values</c> class owns its value set: <c>AllValues</c> (the constants'
/// values, in the constants' order) and <c>Contains</c> (ordinal). Consumers validate
/// against the vocabulary instead of retyping it as a hand-built set. Covered for the
/// package projection (the real shipped assembly) and both consumer projections. The
/// property is not named <c>All</c> because <c>cassandra.consistency.level</c> already
/// declares a constant <c>All</c>; the emitter refuses any such collision.
/// </summary>
public sealed class EnumValueSetTests
{
    [Fact]
    public void Package_Projection_Values_Class_Owns_Its_Value_Set()
    {
        // The stable package: http.request.method's ten stable members (QUERY is development),
        // in the package projection's identifier order.
        HttpAttributes.RequestMethodValues.AllValues.Should().Equal(
            "CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "_OTHER", "PATCH", "POST", "PUT", "TRACE");
        HttpAttributes.RequestMethodValues.AllValues.Should().HaveCount(10);

        HttpAttributes.RequestMethodValues.Contains("GET").Should().BeTrue();
        HttpAttributes.RequestMethodValues.Contains("get").Should().BeFalse("membership is ordinal");
        HttpAttributes.RequestMethodValues.Contains("QUERY").Should().BeFalse("QUERY is development-stability and not in the stable tier");
        HttpAttributes.RequestMethodValues.Contains(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Consumer_Attributes_Projection_Values_Class_Owns_Its_Value_Set()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionAttributes("http")]
            internal static partial class HttpAttributes;
            """;

        var (result, output) = DefinitionsTestHost.Run<SemConvAttributesGenerator>(source, referenceVocabulary: false);
        var generated = result.GeneratedText("HttpAttributes.g.cs");

        generated.Should()
            .Contain("        /// <summary>Every catalogued value, in registry order.</summary>")
            .And.Contain("        public static global::System.Collections.Generic.IReadOnlyList<string> AllValues { get; } = new[] { \"CONNECT\", \"DELETE\", \"GET\", \"HEAD\", \"OPTIONS\", \"PATCH\", \"POST\", \"PUT\", \"TRACE\", \"_OTHER\" };")
            .And.Contain("        /// <summary>Whether <paramref name=\"value\"/> is a catalogued value (ordinal).</summary>")
            .And.Contain("        public static bool Contains(string value) { foreach (var candidate in AllValues) if (string.Equals(candidate, value, global::System.StringComparison.Ordinal)) return true; return false; }");

        var values = LoadType(output, "MyApp.HttpAttributes+HttpRequestMethodValues");
        All(values).Should().Equal("CONNECT", "DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT", "TRACE", "_OTHER");
        Contains(values, "GET").Should().BeTrue();
        Contains(values, "get").Should().BeFalse("membership is ordinal");
    }

    [Fact]
    public void Consumer_Activities_Projection_Values_Class_Owns_Its_Value_Set()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var (result, output) = DefinitionsTestHost.Run<SemConvActivitiesGenerator>(source, referenceVocabulary: false);
        result.GeneratedText("HttpActivityExtensions.g.cs").Should()
            .Contain("public static global::System.Collections.Generic.IReadOnlyList<string> AllValues { get; } = new[] { \"CONNECT\", \"DELETE\", \"GET\", \"HEAD\", \"OPTIONS\", \"PATCH\", \"POST\", \"PUT\", \"TRACE\", \"_OTHER\" };");

        var values = LoadType(output, "MyApp.HttpActivityExtensions+HttpRequestMethodValues");
        All(values).Should().HaveCount(10).And.Contain("GET");
        Contains(values, "GET").Should().BeTrue();
        Contains(values, "get").Should().BeFalse();
    }

    [Fact]
    public void Deprecated_Attribute_Members_Are_Included_In_The_Stable_Set()
    {
        // rpc.grpc.status_code is deprecated; the consumer projection inherits the attribute's
        // deprecation into its (development) members, so the stable tier still emits them and
        // AllValues carries their integer values as strings.
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionAttributes("rpc")]
            internal static partial class RpcAttributes;
            """;

        var (result, output) = DefinitionsTestHost.Run<SemConvAttributesGenerator>(source, referenceVocabulary: false);
        result.GeneratedText("RpcAttributes.g.cs").Should()
            .Contain("public static class RpcGrpcStatusCodeValues")
            .And.Contain("public const string Ok = \"0\";");

        var values = LoadType(output, "MyApp.RpcAttributes+RpcGrpcStatusCodeValues");
        All(values).Should().StartWith("0").And.Contain("10");
        Contains(values, "0").Should().BeTrue();
        Contains(values, "ok").Should().BeFalse("the set holds registry values, not identifiers");
    }

    [Fact]
    public void Empty_Set_Is_Emitted_As_Array_Empty()
    {
        // No stable-tier …Values class is empty in the pinned registry (deprecated attributes
        // carry their members into the stable tier), so the empty path is pinned at the
        // emitter: an inferred `new[] { }` would not compile.
        var lines = Emitters.EnumValueSet.Lines([], [], "EmptyValues", Emitters.EnumValueSet.RegistryOrderSummary);

        lines.Should().HaveCount(4);
        lines[1].Should().Be("public static global::System.Collections.Generic.IReadOnlyList<string> AllValues { get; } = global::System.Array.Empty<string>();");
    }

    [Fact]
    public void Emitter_Refuses_A_Constant_That_Collides_With_The_Value_Set_Members()
    {
        // cassandra.consistency.level has a member `all` (constant `All`): the fixed member
        // names must never shadow a constant, so a collision is a generation-time fault.
        var act = () => Emitters.EnumValueSet.Lines(["All", "AllValues"], ["all", "all_values"], "ConsistencyLevelValues", Emitters.EnumValueSet.RegistryOrderSummary);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Enum value class 'ConsistencyLevelValues' declares a constant 'AllValues', which collides with the generated value-set member of the same name.");

        Emitters.EnumValueSet.Lines(["All", "Any"], ["all", "any"], "ConsistencyLevelValues", Emitters.EnumValueSet.RegistryOrderSummary)
            .Should().HaveCount(4, "a constant named All is fine; only AllValues and Contains are reserved");
    }

    private static Type LoadType(Microsoft.CodeAnalysis.Compilation compilation, string typeName)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        emit.Success.Should().BeTrue(string.Join(Environment.NewLine, emit.Diagnostics));
        return Assembly.Load(stream.ToArray()).GetType(typeName, throwOnError: true)!;
    }

    private static IReadOnlyList<string> All(Type values) =>
        (IReadOnlyList<string>)values.GetProperty("AllValues", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static bool Contains(Type values, string value) =>
        (bool)values.GetMethod("Contains", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [value])!;
}
