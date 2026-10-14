using System.Diagnostics;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Incubating.Activities;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Cpython;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

public sealed class NumericEnumTests
{
    [Fact]
    public void Integer_enum_constants_and_sets_preserve_numeric_values()
    {
        long generation = CpythonAttributes.GcGenerationValues.Generation0;
        generation.Should().Be(0L);
        CpythonAttributes.GcGenerationValues.AllValues.Should().Equal(0L, 1L, 2L);
        CpythonAttributes.GcGenerationValues.Contains(generation).Should().BeTrue();
        CpythonAttributes.GcGenerationValues.Contains(42L).Should().BeFalse();
    }

    [Fact]
    public void Integer_enum_value_sets_keep_the_integer()
    {
        CpythonGcGeneration.Generation1.Value.Should().Be(1L);
        CpythonGcGeneration.Custom(7).Value.Should().Be(7L);
        CpythonGcGeneration.Custom(1).Should().Be(CpythonGcGeneration.Generation1);
    }

    [Fact]
    public void Integer_enum_activity_setter_emits_a_numeric_tag()
    {
        using var activity = new Activity("numeric-enum");
        activity.SetCpythonGcGeneration(CpythonGcGeneration.Generation1);
        activity.GetTagItem(CpythonAttributes.GcGeneration).Should().BeOfType<long>().Which.Should().Be(1L);
    }
}
