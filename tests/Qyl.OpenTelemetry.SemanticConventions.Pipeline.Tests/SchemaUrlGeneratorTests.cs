// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests;

public class SchemaUrlGeneratorTests
{
    [Fact]
    public void Generator_emits_default_schema_url_when_property_unset()
    {
        GeneratorDriverRunResult result = RunGenerator(buildPropertySchemaVersion: null);
        string generated = result.GeneratedTrees.Single().ToString();

        generated.Should().Contain("https://opentelemetry.io/schemas/1.41.0");
        generated.Should().Contain("public const string Current");
        generated.Should().Contain("public const string Version");
    }

    [Fact]
    public void Generator_honours_build_property_override()
    {
        GeneratorDriverRunResult result = RunGenerator(buildPropertySchemaVersion: "1.42.0-dev");
        string generated = result.GeneratedTrees.Single().ToString();

        generated.Should().Contain("https://opentelemetry.io/schemas/1.42.0-dev");
        generated.Should().Contain("\"1.42.0-dev\"");
    }

    [Fact]
    public void Generator_emits_into_Generated_namespace()
    {
        GeneratorDriverRunResult result = RunGenerator(buildPropertySchemaVersion: null);
        string generated = result.GeneratedTrees.Single().ToString();

        generated.Should().Contain("namespace Qyl.OpenTelemetry.SemanticConventions.Generated;");
    }

    static GeneratorDriverRunResult RunGenerator(string? buildPropertySchemaVersion)
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "TestAsm",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("// consumer source") },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) });

        Dictionary<string, string> globalOptions = new();
        if (buildPropertySchemaVersion is not null)
            globalOptions["build_property.QylSemConvSchemaVersion"] = buildPropertySchemaVersion;

        var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);

        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new SchemaUrlGenerator())
            .WithUpdatedAnalyzerConfigOptions(optionsProvider);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        readonly TestOptions globalOptions;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> options)
            => globalOptions = new TestOptions(options);

        public override AnalyzerConfigOptions GlobalOptions => globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestOptions.Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => TestOptions.Empty;
    }

    sealed class TestOptions : AnalyzerConfigOptions
    {
        public static readonly TestOptions Empty = new(ImmutableDictionary<string, string>.Empty);

        readonly IReadOnlyDictionary<string, string> values;

        public TestOptions(IReadOnlyDictionary<string, string> values) => this.values = values;

        public override bool TryGetValue(string key, out string value)
        {
            if (values.TryGetValue(key, out string? v))
            {
                value = v;
                return true;
            }
            value = string.Empty;
            return false;
        }
    }
}
