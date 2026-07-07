// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System;
using AwesomeAssertions;
using Qyl.OpenTelemetry.SemanticConventions.Nuke;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

public class LockstepPolicyTests
{
    [Theory]
    [InlineData("1.43.0-1", "1.43.0", 1)]
    [InlineData("1.43.0-99", "1.43.0", 99)]
    [InlineData("1.0.0-rc.1-1", "1.0.0-rc.1", 1)]
    public void ParseSemconvSuffixVersion_returns_components_for_valid_input(
        string input, string expectedSemconv, int expectedN)
    {
        (string semconv, int n) = LockstepPolicy.ParseSemconvSuffixVersion(input);

        semconv.Should().Be(expectedSemconv);
        n.Should().Be(expectedN);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.43.0")]
    [InlineData("1.43.0-")]
    [InlineData("-1")]
    [InlineData("1.43.0-abc")]
    [InlineData("1.43.0-0")]
    public void ParseSemconvSuffixVersion_throws_FormatException_for_malformed_input(string input)
    {
        Action action = () => LockstepPolicy.ParseSemconvSuffixVersion(input);

        action.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParseSemconvSuffixVersion_uses_last_hyphen_as_separator()
    {
        // Documented behavior: LastIndexOf('-') wins, so a prerelease semconv segment
        // like "1.0.0-rc.1-7" parses as semconv="1.0.0-rc.1" n=7. The corollary is that
        // "1.43.0--1" parses as semconv="1.43.0-" n=1 even though the trailing dash is
        // odd; callers should validate the semconv shape themselves if that matters.
        (string semconv, int n) = LockstepPolicy.ParseSemconvSuffixVersion("1.43.0--1");

        semconv.Should().Be("1.43.0-");
        n.Should().Be(1);
    }

    [Fact]
    public void ParseSemconvSuffixVersion_throws_ArgumentNullException_for_null_input()
    {
        Action action = () => LockstepPolicy.ParseSemconvSuffixVersion(null!);

        action.Should().Throw<ArgumentNullException>();
    }
}
