using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Loader-level gate for enum-member values. A few registry enums carry integer values
/// (<c>cpython.gc.generation</c>, <c>rpc.grpc.status_code</c>); every projection emits them
/// as string constants spelled exactly like the JSON scalar, so the loaders must not read
/// them through the string accessor (which yields an empty string for a number).
/// </summary>
public sealed class RegistryLoaderTests
{
    [Fact]
    public void ScalarToString_Spells_Json_Scalars_As_The_Registry_Does()
    {
        RegistryParsing.ScalarToString(new JsonNumber("0")).Should().Be("0");
        RegistryParsing.ScalarToString(new JsonNumber("10")).Should().Be("10");
        RegistryParsing.ScalarToString(new JsonNumber("1.5")).Should().Be("1.5");
        RegistryParsing.ScalarToString(new JsonString("read")).Should().Be("read");
        RegistryParsing.ScalarToString(new JsonBool(true)).Should().Be("true");
        RegistryParsing.ScalarToString(JsonNull.Instance).Should().BeEmpty();
        RegistryParsing.ScalarToString(null).Should().BeEmpty();
    }

    [Theory]
    [InlineData("cpython.gc.generation", "generation_0", "0")]
    [InlineData("cpython.gc.generation", "generation_2", "2")]
    [InlineData("rpc.grpc.status_code", "ok", "0")]
    [InlineData("rpc.grpc.status_code", "aborted", "10")]
    public void Attribute_Registry_Keeps_Integer_Enum_Member_Values(string key, string memberId, string expectedValue)
    {
        var attribute = RegistryLoader.Registry.Catalog.ToArray().Single(a => a.Key == key);
        var enumType = attribute.Type.Should().BeOfType<AttributeTypeModel.EnumType>().Subject;

        enumType.Members.ToArray().Single(m => m.Id == memberId).Value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData("cpython.gc.generation", "generation_1", "1")]
    [InlineData("rpc.grpc.status_code", "cancelled", "1")]
    public void Activity_Registry_Keeps_Integer_Enum_Member_Values(string key, string memberId, string expectedValue)
    {
        var attribute = ActivityRegistryLoader.Registry.Attributes.ToArray().Single(a => a.Key == key);

        attribute.IsEnum.Should().BeTrue();
        attribute.EnumMembers.ToArray().Single(m => m.Id == memberId).Value.Should().Be(expectedValue);
    }
}
