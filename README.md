# Qyl.Telemetry.SemanticConventions

.NET packages generated from pinned OpenTelemetry semantic-convention registries.
The current registry projection combines the pinned core registry with the separately
pinned development GenAI registry. [`Version.props`](Version.props) is the single
source of truth for those upstream pins.

This is the one artifact both halves of qyl share: what an application emits and what
the collector recognizes are generated from the same resolved registry, so qyl cannot
emit an attribute name its own collector does not know.

## Packages

| Package | Contents |
| --- | --- |
| `Qyl.Telemetry.SemanticConventions` | Stable and deprecated attribute-key constants (every enum-value class carries `AllValues` and `Contains`), plus the semantic-convention definition types (`MetricDefinition<TInstrument>`, `SpanDefinition<TKind>`, `EventDefinition`, `EntityDefinition`, and their `Stability`/`Deprecation`/`RequirementLevel`/`AttributeRef`/`EntityRef` companions) |
| `.Incubating` | Development and unstable constants, the complete resolved registry, and upstream GenAI payload schemas |
| `.SourceGeneration` | Roslyn generators for typed telemetry declarations; its definition surfaces are typed against the base package |
| `.Analyzers` | Roslyn diagnostics and code fixes for semantic-convention consumers, with a generated rule catalog and severity profiles |

```bash
dotnet add package Qyl.Telemetry.SemanticConventions
```

All four packages are published on
[nuget.org](https://www.nuget.org/profiles/ANcpLua); the package version is defined in
[`Directory.Build.props`](Directory.Build.props). Incubating APIs intentionally track
unstable upstream conventions and may change between minor releases.

**Coming from `Qyl.OpenTelemetry.SemanticConventions`?** These are new package IDs, not
new versions of the old ones. The `Qyl.OpenTelemetry.SemanticConventions*` IDs stop at
`4.0.0` and receive no further releases; change to the `Qyl.Telemetry.*` family. The new
family began at `1.0.0` and now follows its own monotonic release line.

## Choosing between the compiled packages and the source generator

Both modes share one vocabulary. `Qyl.Telemetry.SemanticConventions` is the home of the
object-first definition types — `MetricDefinition<TInstrument>`, `SpanDefinition<TKind>`,
`EventDefinition`, `EntityDefinition`, `Stability`, `Deprecation`, `RequirementLevel`,
`AttributeRef`, `EntityRef`, and the instrument/span-kind marker structs — so a definition
generated in any assembly is an instance of the same public type and can be handed
between libraries and applications.

**Libraries reference the compiled packages** (`Qyl.Telemetry.SemanticConventions`,
`.Incubating`). A library needs one pinned registry version across its whole package
family and a public constant surface to alias against
(`Qyl.Telemetry.AutoInstrumentation` does this through its internal
`QylSemanticAttributes` facade). Because the constants are `const string`, the
compiler inlines them at every use site — the reference costs effectively nothing
at runtime.

**The compiled packages are consumers of the generator.** Their constant classes are
projected from `resolved-registry.json` at build time by the assembly-level package markers
(`[assembly: SemanticConventionAttributesPackage(...)]` and siblings; see the generator
README), so both consumption modes are one emitter over one registry.

**Applications may use either.** Declaring
`[SemanticConventionAttributes("http")] internal static partial class HttpAttributes;`
emits only the requested groups directly into the consuming assembly with internal
visibility (`qyl`'s `Qyl.Run.Workload` consumes it this way). The definition surfaces
(`[SemanticConventionMetricDefinitions]`, `[SemanticConventionSpanDefinitions]`,
`[SemanticConventionEventDefinitions]`, `[SemanticConventionEntityDefinitions]`) are
typed against `Qyl.Telemetry.SemanticConventions`: reference it next to the generator,
or the generator reports `QYLSG001` at the marker.

## Generation pipeline

```text
pinned OpenTelemetry registries + qyl-registry.json
        |
        v
generate.sh -> resolved-registry.json (core + genai + qyl, one vocabulary model)
        |
        +----> Roslyn generators -----> compiled packages (package projection, at build)
        |                        \----> consumer telemetry helpers and definitions
        +----> emit_registry_resources.py -> public registry + GenAI JSON Schemas
        +----> emit_analyzer_registry.py --> registry-derived analyzer facts
        +----> emit_typespec_keys.py -> qyl-api-schema key projection
        +----> DocsGenerator ---------> analyzer documentation
```

There is one vocabulary model (`resolved-registry.json`, the merged core + GenAI + qyl
projection) and one C# emitter (the Roslyn generators). The compiled packages contain no
checked-in constants: `Qyl.Telemetry.SemanticConventions` and `.Incubating` reference the
generator as an analyzer and declare assembly-level package markers. The shipped surface is
pinned by the generator's `PackageProjectionTests`: five full-file byte-identity snapshots
(`http` stable and incubating, `qyl`, `QylTelemetryNames`, `SchemaUrl`) plus
`Snapshots/qyl.package.manifest.sha256`, one SHA-256 line for every file both projections
emit (every `{Root}Attributes` class of both tiers, `SchemaUrl`, `QylTelemetryNames`); a
mismatch names each differing, missing, or extra file, and only `REGEN_SNAPSHOTS`
rewrites them. The other generated files carry their owning script and are guarded by
`--check` commands or generated documentation checks.

### Vendor models

`qyl-registry.json` owns one more thing than qyl's own vocabulary: the keys a pinned
third-party library emits on its own `ActivitySource` or `Meter` and that upstream
semantic conventions do not define. The collector's attribute allowlist is generated
from the registry, so a key nothing declares is dropped at ingest — which is exactly
what happens the moment an application stops wrapping a library and subscribes to its
native source instead.

The merge refuses any attribute outside `qyl.*` unless a `vendor_models` entry declares
it. There is no prefix allowlist: a vendor model names the library, the exact version
qyl pins, the repository and tag its attributes were read at, the licence, and the
`ActivitySource` names it emits on — and every attribute in it carries the file and line
of the library that sets the key. A model that cannot answer those questions fails
generation, and so does a vendor key that shadows an upstream row. Vendor rows are
`development` stability, carry the qyl provenance (the finding is written down in
`qyl-registry.json`, whose SHA-256 is the source commit), and stay out of the TypeSpec
projection, which is the upstream key surface.

The ActivitySource names themselves are registry facts too: they land in
`vendor_scope_names`, ship as `QylTelemetryNames.VendorActivitySources`, and join
QYL0200's allowlist, so `AddSource` and a span processor's source match need no literal.

The incubating package exposes the complete source-attributed resolved model — vendor
models and all — through `SemanticConventionRegistry.OpenResolvedRegistry()`. The eight structured GenAI
`type: any` attributes expose their exact upstream JSON Schemas through
`TryOpenPayloadSchema`. These raw schemas are the payload contract; the package does
not invent parallel DTOs or a partial JSON Schema implementation.

The TypeSpec projection contains semantic-convention key names only. Qyl's
client-visible product requests, responses, stream events, and errors remain owned by
[`qyl-api-schema`](https://github.com/ANcpLua/qyl-api-schema).

## Analyzer documentation

The analyzer project exposes a generated rule catalog and ships as the fourth released
package; `eng/release/verify-packages.sh` proves it installs into a clean consumer
alongside the other three.

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

### QYL0008 in instrumentation libraries

[`QYL0008`](docs/rules/QYL0008_IncubatingSemconvInLibrary.md) flags a direct reference to
an `*.SemanticConventions.Incubating` member from a library project, because the library
would then pin every downstream consumer to its exact package version. Copying the value
into the library is the mitigation, and the analyzer stays silent for all three copy
forms plus a method-local `const`:

```csharp
private const string MessagingSystem = MessagingAttributes.System;
private static readonly string OperationType = MessagingAttributes.OperationType;
private static readonly string[] Copies = [MessagingAttributes.OperationName];

public static void Tag(Activity activity)
{
    const string destination = MessagingAttributes.DestinationName;
    activity.SetTag(destination, "orders");
}
```

An instrumentation library that deliberately version-locks with the incubating tier —
`Qyl.Telemetry.AutoInstrumentation` ships in lockstep with this package family — opts the
whole project out instead of copying every constant:

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <OtelSemConvInstrumentationLibrary>true</OtelSemConvInstrumentationLibrary>
  </PropertyGroup>
</Project>
```

The property is opt-in and per-project; QYL0008 keeps reporting everywhere it is unset.

## Build and test

```bash
dotnet build Qyl.Telemetry.SemanticConventions.slnx -c Release
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.Pipeline.Tests -c Release
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests -c Release
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/verify_deprecated_catalog.py
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_analyzer_registry.py --check
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_registry_resources.py --check
```

Publishing uses GitHub Actions OIDC trusted publishing. No long-lived NuGet API key
is stored in the repository.

## License

Apache-2.0
