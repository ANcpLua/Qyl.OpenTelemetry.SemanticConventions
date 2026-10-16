// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using Qyl.Telemetry.SemanticConventions.Analyzers;

namespace Qyl.Telemetry.SemanticConventions.Pipeline.Tests;
[TestClass]
public sealed class AttributeKeyPatternSetTests
{
    [TestMethod]
    [DataRow("api_key", "api,key")]
    [DataRow("api.key", "api,key")]
    [DataRow("apiKey", "api,key")]
    [DataRow("x-api-key", "x,api,key")]
    [DataRow("HTTPToken", "http,token")]
    [DataRow("gen_ai.usage.input_tokens", "gen,ai,usage,input,tokens")]
    [DataRow("userID2", "user,id2")]
    public void Keys_split_on_separators_and_camel_case(string key, string expected)
    {
        Assert.AreEqual(expected, string.Join(",", AttributeKeyPatternSet.Segment(key)));
    }

    [TestMethod]
    [DataRow("auth.token", true)]
    [DataRow("x-api-key", true)]
    [DataRow("apiKey", true)]
    [DataRow("card.pin", true)]
    [DataRow("gen_ai.usage.input_tokens", false)]
    [DataRow("cicd.pipeline.run.id", false)]
    [DataRow("app.author.name", false)]
    [DataRow("", false)]
    public void Patterns_match_contiguous_segment_runs_only(string key, bool expected)
    {
        var patterns = new AttributeKeyPatternSet("token", "pin", "auth", "api_key");
        Assert.AreEqual(expected, patterns.Matches(key));
    }
}
