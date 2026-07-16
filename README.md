# Qyl.OpenTelemetry.SemanticConventions

.NET packages generated from pinned OpenTelemetry semantic-convention registries.
The current registry projection targets core semantic conventions 1.43.0 plus the
separately pinned development GenAI registry.

## Packages

| Package | Contents |
| --- | --- |
| `Qyl.OpenTelemetry.SemanticConventions` | Stable and deprecated attribute-key constants |
| `.Incubating` | Development and unstable constants, the complete resolved registry, and upstream GenAI payload schemas |
| `.SourceGeneration` | Roslyn generators for typed telemetry declarations |
| `.Analyzers` | Preview diagnostics and code fixes; built and documented, but excluded from stable releases |

The supported package set is published on
[nuget.org](https://www.nuget.org/profiles/ANcpLua). Incubating APIs intentionally
track unstable upstream conventions and may change between minor releases.

## Generation pipeline

```text
pinned OpenTelemetry registries
        |
        v
generate.sh -> resolved-registry.json
        |
        +----> emit_attributes.py ----> stable/incubating C# constants
        +----> emit_registry_resources.py -> public registry + GenAI JSON Schemas
        +----> emit_analyzer_registry.py --> registry-derived analyzer facts
        +----> Roslyn generators -----> consumer telemetry helpers
        +----> emit_typespec_keys.py -> qyl-api-schema key projection
        +----> DocsGenerator ---------> analyzer documentation
```

The checked-in C# constants are emitted deterministically from the Weaver-resolved
registry; they are not handwritten and are not emitted directly by Weaver. Generated
files carry their owning input/generator and are guarded by build hashes or generated
documentation checks.

The incubating package exposes the complete source-attributed resolved model through
`SemanticConventionRegistry.OpenResolvedRegistry()`. The eight structured GenAI
`type: any` attributes expose their exact upstream JSON Schemas through
`TryOpenPayloadSchema`. These raw schemas are the payload contract; the package does
not invent parallel DTOs or a partial JSON Schema implementation.

The TypeSpec projection contains semantic-convention key names only. Qyl's
client-visible product requests, responses, stream events, and errors remain owned by
[`qyl-api-schema`](https://github.com/ANcpLua/qyl-api-schema).

## Analyzer documentation

The analyzer project defines 48 rules. Qyl does not consume it, and seven rules have
executable behavior tests. It therefore remains preview-only and is
not included in stable package builds. `PackPreviewAnalyzers=true` enables an explicit
prerelease pack; the build rejects a stable analyzer version.

The generated analyzer reference includes the
[`index`](docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md),
[`migration catalog`](docs/migration-catalog.md), and
[`per-rule pages`](docs/rules/). Consumer severity can be configured through
`OtelSemConvAnalysisMode`:

```xml
<PropertyGroup>
  <OtelSemConvAnalysisMode>AllAsErrors</OtelSemConvAnalysisMode>
</PropertyGroup>
```

Supported values are `Default`, `AllAsErrors`, and `Disabled`. If the property is
unset, the consumer's editorconfig remains authoritative.

## Build and test

```bash
dotnet build Qyl.OpenTelemetry.SemanticConventions.slnx -c Release
dotnet run --project tests/Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests -c Release
dotnet run --project tests/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests -c Release
./build.sh VerifyAttributesHash
./build.sh CheckDocs
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/verify_deprecated_catalog.py
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/emit_analyzer_registry.py --check
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/emit_registry_resources.py --check
```

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet API key
is stored in the repository.

## License

Apache-2.0
