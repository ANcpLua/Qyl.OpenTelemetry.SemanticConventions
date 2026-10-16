using System.Diagnostics;
using Qyl.Telemetry.SemanticConventions.Incubating.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Cpython;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
[TestClass]
public sealed class NumericEnumTests
{
    [TestMethod]
    public void Integer_enum_constants_and_sets_preserve_numeric_values()
    {
        long generation = CpythonAttributes.GcGenerationValues.Generation0;
        Assert.AreEqual(0L, generation);
        Assert.AreSequenceEqual([0L, 1L, 2L], CpythonAttributes.GcGenerationValues.AllValues);
        Assert.IsTrue(CpythonAttributes.GcGenerationValues.Contains(generation));
        Assert.IsFalse(CpythonAttributes.GcGenerationValues.Contains(42L));
    }

    [TestMethod]
    public void Integer_enum_value_sets_keep_the_integer()
    {
        Assert.AreEqual(1L, CpythonGcGeneration.Generation1.Value);
        Assert.AreEqual(7L, CpythonGcGeneration.Custom(7).Value);
        Assert.AreEqual(CpythonGcGeneration.Generation1, CpythonGcGeneration.Custom(1));
    }

    [TestMethod]
    public void Integer_enum_activity_setter_emits_a_numeric_tag()
    {
        using var activity = new Activity("numeric-enum");
        activity.SetCpythonGcGeneration(CpythonGcGeneration.Generation1);
        Assert.AreEqual(1L, Assert.IsInstanceOfType<long>(activity.GetTagItem(CpythonAttributes.GcGeneration)));
    }
}
