using System.Diagnostics;
using AwesomeAssertions;
using Qyl;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

/// <summary>
/// Business keys go through <c>SetAppTag</c>, which writes the registry's <c>qyl.app</c> template
/// key; the caller never types the prefix (ADR D3, answer 2).
/// </summary>
public sealed class AppTagTests
{
    [Fact]
    public void SetAppTag_writes_under_the_qyl_app_prefix()
    {
        using var activity = new Activity("app-tag");

        activity.SetAppTag("order.id", "ord-1042").Should().BeSameAs(activity);

        activity.GetTagItem("qyl.app.order.id").Should().Be("ord-1042");
        activity.GetTagItem("order.id").Should().BeNull();
    }

    [Fact]
    public void SetAppTag_keeps_long_double_and_bool_values_typed()
    {
        using var activity = new Activity("app-tag");

        activity.SetAppTag("order.lines", 3L).SetAppTag("order.total", 41.5).SetAppTag("order.express", true);

        activity.GetTagItem("qyl.app.order.lines").Should().BeOfType<long>().Which.Should().Be(3L);
        activity.GetTagItem("qyl.app.order.total").Should().BeOfType<double>().Which.Should().Be(41.5);
        activity.GetTagItem("qyl.app.order.express").Should().BeOfType<bool>().Which.Should().BeTrue();
    }
}
