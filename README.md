# Qyl.Telemetry.SemanticConventions

.NET packages generated from pinned OpenTelemetry semantic-convention registries.
The current registry projection targets core semantic conventions 1.43.0 plus the
separately pinned development GenAI registry.

This is the one artifact both halves of qyl share: what an application emits and what
the collector recognizes are generated from the same resolved registry, so qyl cannot
emit an attribute name its own collector does not know.

## Packages

| Package | Contents |
| --- | --- |
| `Qyl.Telemetry.SemanticConventions` | Stable and deprecated attribute-key constants |
| `.Incubating` | Development and unstable constants, the complete resolved registry, and upstream GenAI payload schemas |
| `.SourceGeneration` | Roslyn generators for typed telemetry declarations |
| `.Analyzers` | Preview diagnostics and code fixes; built and documented, but excluded from stable releases |

```bash
dotnet add package Qyl.Telemetry.SemanticConventions
```

The three supported packages are published on
[nuget.org](https://www.nuget.org/profiles/ANcpLua); this change targets `4.2.0`. `.Analyzers` is
preview-only and is not among them. Incubating APIs intentionally track unstable
upstream conventions and may change between minor releases.

**Coming from `Qyl.OpenTelemetry.SemanticConventions`?** These are new package IDs, not
new versions of the old ones. The `Qyl.OpenTelemetry.SemanticConventions*` IDs stop at
`4.0.0` and receive no further releases; change to the `Qyl.Telemetry.*` family. The new
family began at `1.0.0` and now follows its own monotonic release line.

## Choosing between the compiled packages and the source generator

The registry ships in two consumption modes; pick by what you are building.

**Libraries reference the compiled packages** (`Qyl.Telemetry.SemanticConventions`,
`.Incubating`). A library needs one pinned registry version across its whole package
family and a stable public constant surface to alias against
(`Qyl.Telemetry.AutoInstrumentation` does this through its internal
`QylSemanticAttributes` facade). Because the constants are `const string`, the
compiler inlines them at every use site — the reference costs effectively nothing
at runtime.

**Applications use `.SourceGeneration`.** Declaring
`[SemanticConventionAttributes("http")] internal static partial class HttpAttributes;`
emits only the requested groups directly into the consuming assembly — internal
visibility, no package in the dependency chain, tree-shaken by construction
(`qyl`'s `Qyl.Run.Workload` consumes it this way).

Do not switch a library from the compiled packages to the generator: that moves the
registry pin from per-package-release to per-consumer-compilation and floods the
library's own generator snapshots with foreign generated files.

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
[`index`](docs/Qyl.Telemetry.SemanticConventions.Analyzers.md),
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
dotnet build Qyl.Telemetry.SemanticConventions.slnx -c Release
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.Pipeline.Tests -c Release
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests -c Release
./build.sh VerifyAttributesHash
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/verify_deprecated_catalog.py
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_analyzer_registry.py --check
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_registry_resources.py --check
```

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet API key
is stored in the repository.

## License

Apache-2.0
