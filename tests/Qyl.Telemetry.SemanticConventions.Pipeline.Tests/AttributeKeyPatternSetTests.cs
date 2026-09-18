// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.Analyzers;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;

public sealed class AttributeKeyPatternSetTests
{
    [Theory]
    [InlineData("api_key", "api,key")]
    [InlineData("api.key", "api,key")]
    [InlineData("apiKey", "api,key")]
    [InlineData("x-api-key", "x,api,key")]
    [InlineData("HTTPToken", "http,token")]
    [InlineData("gen_ai.usage.input_tokens", "gen,ai,usage,input,tokens")]
    [InlineData("userID2", "user,id2")]
    public void Keys_split_on_separators_and_camel_case(string key, string expected)
    {
        string.Join(",", AttributeKeyPatternSet.Segment(key)).Should().Be(expected);
    }

    [Theory]
    [InlineData("auth.token", true)]
    [InlineData("x-api-key", true)]
    [InlineData("apiKey", true)]
    [InlineData("card.pin", true)]
    [InlineData("gen_ai.usage.input_tokens", false)]
    [InlineData("cicd.pipeline.run.id", false)]
    [InlineData("app.author.name", false)]
    [InlineData("", false)]
    public void Patterns_match_contiguous_segment_runs_only(string key, bool expected)
    {
        var patterns = new AttributeKeyPatternSet("token", "pin", "auth", "api_key");

        patterns.Matches(key).Should().Be(expected);
    }
}
