using System.Diagnostics;
using System.Reflection;
using ANcpLua.Roslyn.Utilities.Testing;
using Qyl.Telemetry.SemanticConventions.Activities;
using Qyl.Telemetry.SemanticConventions.Attributes.Db;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
/// <summary>
/// Registry enums are open value sets: a readonly struct with one static member per value and an
/// explicit <c>Custom</c> escape hatch, no implicit conversion from <see cref = "string "/>, and typed
/// setters that take the struct. The <c>const string</c> classes under Generated/Attributes stay,
/// because <c>switch</c> and pattern matching need constants.
/// </summary>
[TestClass]
public sealed class ValueSetStructTests
{
    [TestMethod]
    public void A_member_carries_the_registry_value()
    {
        Assert.AreEqual("postgresql", DbSystemName.Postgresql.Value);
        Assert.AreEqual("db.system.name", typeof(DbSystemName).GetField(nameof(DbSystemName.Key))!.GetRawConstantValue());
        Assert.AreEqual("_OTHER", ErrorType.Other.Value);
    }

    [TestMethod]
    public void Custom_carries_a_value_the_registry_does_not_list_and_compares_by_value()
    {
        Assert.AreEqual("memory", DbSystemName.Custom("memory").Value);
        Assert.AreEqual(DbSystemName.Postgresql, DbSystemName.Custom("postgresql"));
        Assert.IsTrue((DbSystemName.Custom("postgresql") == DbSystemName.Postgresql));
        Assert.IsTrue((DbSystemName.Custom("memory") != DbSystemName.Postgresql));
        Assert.AreEqual("postgresql", DbSystemName.Postgresql.ToString());
    }

    [TestMethod]
    public void A_string_literal_does_not_convert_to_a_value_set()
    {
        var result = Compile.Source("""
                using Qyl.Telemetry.SemanticConventions.Activities;

                public static class Store
                {
                    public static DbSystemName System = "postgresql";
                }
                """).WithReference(typeof(DbSystemName).Assembly).Build();
        Assert.IsTrue(result.Failed);
        Assert.IsTrue(result.HasError("CS0029"));
    }

    [TestMethod]
    public void The_typed_setter_takes_the_struct_and_writes_its_value()
    {
        using var activity = new Activity("value-set");
        activity.SetDbSystemName(DbSystemName.Custom("memory"));
        Assert.AreEqual("memory", activity.GetTagItem("db.system.name"));
        Assert.AreEqual(typeof(DbSystemName), typeof(DbActivityExtensions).GetMethod(nameof(DbActivityExtensions.SetDbSystemName))!.GetParameters()[1].ParameterType);
    }

    [TestMethod]
    public void The_activity_value_classes_are_gone_and_the_attribute_constants_stay()
    {
        var nestedValueClasses = new[]
        {
            typeof(DbActivityExtensions).Assembly,
            typeof(Incubating.Activities.GenAiIncubatingActivityExtensions).Assembly
        }.SelectMany(static assembly => assembly.GetTypes()).Where(static type => type.Namespace?.EndsWith(".Activities", StringComparison.Ordinal) == true && type.IsNested && type.Name.EndsWith("Values", StringComparison.Ordinal)).Select(static type => type.FullName).ToList();
        Assert.IsEmpty(nestedValueClasses);
        Assert.AreEqual("postgresql", typeof(DbAttributes.SystemNameValues).GetField(nameof(DbAttributes.SystemNameValues.Postgresql))!.GetRawConstantValue());
    }

    [TestMethod]
    public void No_setter_is_generated_for_a_resource_only_attribute()
    {
        var setters = new[]
        {
            typeof(DbActivityExtensions).Assembly,
            typeof(Incubating.Activities.GenAiIncubatingActivityExtensions).Assembly
        }.SelectMany(static assembly => assembly.GetTypes()).Where(static type => type.Namespace?.EndsWith(".Activities", StringComparison.Ordinal) == true).SelectMany(static type => type.GetMethods(BindingFlags.Public | BindingFlags.Static)).Select(static method => method.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("SetDbSystemName", setters);
        Assert.Contains("SetHttpRequestMethod", setters);
        Assert.DoesNotContain("SetServiceName", setters);
        Assert.DoesNotContain("SetDeploymentEnvironmentName", setters);
        Assert.DoesNotContain("SetHostName", setters);
        Assert.DoesNotContain("SetQylApp", setters);
    }
}
