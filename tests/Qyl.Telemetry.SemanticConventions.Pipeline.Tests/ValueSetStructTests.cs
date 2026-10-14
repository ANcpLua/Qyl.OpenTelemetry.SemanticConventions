using System.Diagnostics;
using System.Reflection;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Attributes.Db;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Registry enums are open value sets: a readonly struct with one static member per value and an
/// explicit <c>Custom</c> escape hatch, no implicit conversion from <see cref="string"/>, and typed
/// setters that take the struct. The <c>const string</c> classes under Generated/Attributes stay,
/// because <c>switch</c> and pattern matching need constants.
/// </summary>
public sealed class ValueSetStructTests
{
    [Fact]
    public void A_member_carries_the_registry_value()
    {
        DbSystemName.Postgresql.Value.Should().Be("postgresql");
        DbSystemName.Key.Should().Be("db.system.name");
        ErrorType.Other.Value.Should().Be("_OTHER");
    }

    [Fact]
    public void Custom_carries_a_value_the_registry_does_not_list_and_compares_by_value()
    {
        DbSystemName.Custom("memory").Value.Should().Be("memory");
        DbSystemName.Custom("postgresql").Should().Be(DbSystemName.Postgresql);
        (DbSystemName.Custom("postgresql") == DbSystemName.Postgresql).Should().BeTrue();
        (DbSystemName.Custom("memory") != DbSystemName.Postgresql).Should().BeTrue();
        DbSystemName.Postgresql.ToString().Should().Be("postgresql");
    }

    [Fact]
    public void A_string_literal_does_not_convert_to_a_value_set()
    {
        var result = Compile
            .Source("""
                using Qyl.Telemetry.SemanticConventions.Activities;

                public static class Store
                {
                    public static DbSystemName System = "postgresql";
                }
                """)
            .WithReference(typeof(DbSystemName).Assembly)
            .Build();

        result.Failed.Should().BeTrue();
        result.HasError("CS0029").Should().BeTrue();
    }

    [Fact]
    public void The_typed_setter_takes_the_struct_and_writes_its_value()
    {
        using var activity = new Activity("value-set");

        activity.SetDbSystemName(DbSystemName.Custom("memory"));

        activity.GetTagItem("db.system.name").Should().Be("memory");
        typeof(DbActivityExtensions).GetMethod(nameof(DbActivityExtensions.SetDbSystemName))!
            .GetParameters()[1].ParameterType.Should().Be<DbSystemName>();
    }

    [Fact]
    public void The_activity_value_classes_are_gone_and_the_attribute_constants_stay()
    {
        var nestedValueClasses = new[] { typeof(DbActivityExtensions).Assembly, typeof(Incubating.Activities.GenAiIncubatingActivityExtensions).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Namespace?.EndsWith(".Activities", StringComparison.Ordinal) == true
                                  && type.IsNested && type.Name.EndsWith("Values", StringComparison.Ordinal))
            .Select(static type => type.FullName)
            .ToList();

        nestedValueClasses.Should().BeEmpty();

        const string postgresql = DbAttributes.SystemNameValues.Postgresql;
        postgresql.Should().Be("postgresql");
    }

    [Fact]
    public void No_setter_is_generated_for_a_resource_only_attribute()
    {
        var setters = new[] { typeof(DbActivityExtensions).Assembly, typeof(Incubating.Activities.GenAiIncubatingActivityExtensions).Assembly }
            .SelectMany(static assembly => assembly.GetTypes())
            .Where(static type => type.Namespace?.EndsWith(".Activities", StringComparison.Ordinal) == true)
            .SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        setters.Should().Contain("SetDbSystemName").And.Contain("SetHttpRequestMethod");
        setters.Should().NotContain("SetServiceName")
            .And.NotContain("SetDeploymentEnvironmentName")
            .And.NotContain("SetHostName")
            .And.NotContain("SetQylApp");
    }
}
