# Qyl.Telemetry.SemanticConventions.SourceGeneration

Roslyn source generator for OpenTelemetry semantic-convention constants,
first-class definitions, and thin helper APIs. It does not collect telemetry.
Consumers own their `Meter`, `ActivitySource`, `Logger`, instrumentation scope,
versioning, and enablement.

## When to use this package

This is the **application-side** consumption mode: the generator emits only the
declared groups into your own assembly, with internal visibility and nothing
you did not ask for. Libraries that need one pinned registry version across a
package family (for example `Qyl.Telemetry.AutoInstrumentation`) reference
the compiled `Qyl.Telemetry.SemanticConventions` packages instead; see the
repository README's "Choosing between the compiled packages and the source
generator" section.

The definition surfaces (metrics, spans, events, entities) are typed against
`Qyl.Telemetry.SemanticConventions`, the one home of `MetricDefinition<TInstrument>`,
`SpanDefinition<TKind>`, `EventDefinition`, `EntityDefinition`, `Stability`,
`Deprecation`, `RequirementLevel`, `AttributeRef`, `EntityRef`, and the
instrument/span-kind marker structs. Reference that package next to the
generator; a compilation that declares a definition marker without it gets
`QYLSG001` at the marker instead of generated source. The attribute-constant
and `Activity` setter surfaces have no runtime package dependency.

```xml
<ItemGroup>
  <PackageReference Include="Qyl.Telemetry.SemanticConventions" Version="..." />
  <PackageReference Include="Qyl.Telemetry.SemanticConventions.SourceGeneration" Version="..."
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## Use

```csharp
using Qyl.Telemetry.SemanticConventions.SourceGeneration;

[SemanticConventionAttributes("http")]
internal static partial class HttpAttributes;

[SemanticConventionIncubatingAttributes("http")]
internal static partial class HttpIncubatingAttributes;

[SemanticConventionActivities("http")]
internal static partial class HttpActivityExtensions;

[SemanticConventionMetricDefinitions("http.server")]
internal static partial class HttpServerMetrics;

[SemanticConventionSpanDefinitions("http")]
internal static partial class HttpSpans;

[SemanticConventionIncubatingEventDefinitions("app")]
internal static partial class AppEvents;

[SemanticConventionIncubatingEntityDefinitions("host")]
internal static partial class HostEntities;

// Generated:
//   public const string AttributeHttpRequestMethod = "http.request.method";
//   public static Activity SetHttpRoute(this Activity activity, string value)
//   public static readonly MetricDefinition<Histogram> HttpServerRequestDuration = new(name: "http.server.request.duration", ...);
//   public static readonly SpanDefinition<Server> HttpServer = new(id: "http.server", ...);
//   public static readonly EventDefinition AppCrash = new(name: "app.crash", ...);
//   public static readonly EntityDefinition Host = new(name: "host", ...);
```

## Generator surfaces

Every generator uses the same Roslyn shape (`GeneratorPipeline`): publish its
stable and incubating marker attributes during post-initialization, discover
annotated partial classes with `ForAttributeWithMetadataName`, extract the
requested semantic-convention prefix into a marker model, then emit source from
the matching registry projection.

| Surface | Marker attributes | Registry projection | Emitter | Generated shape |
|---|---|---|---|---|
| Attributes | `SemanticConventionAttributes`, `SemanticConventionIncubatingAttributes` | `RegistryLoader.Registry` | `AttributesEmitter` | Attribute-key constants and enum-value helper classes (contrib member shape, plus the value set below). |
| Activities | `SemanticConventionActivities`, `SemanticConventionIncubatingActivities` | `ActivityRegistryLoader.Registry` | `ActivityExtensionsEmitter` | `Activity` extension methods that set typed semantic tags, with the same enum-value classes. |
| Metric definitions | `SemanticConventionMetricDefinitions`, `SemanticConventionIncubatingMetricDefinitions` | `RegistryLoader.Instruments` | `MetricDefinitionsEmitter` | `MetricDefinition<TInstrument>` fields: name, unit, stability, deprecation, entity and attribute references. |
| Span definitions | `SemanticConventionSpanDefinitions`, `SemanticConventionIncubatingSpanDefinitions` | `RegistryLoader.Signals` | `SpanDefinitionsEmitter` | `SpanDefinition<TKind>` fields. |
| Event definitions | `SemanticConventionEventDefinitions`, `SemanticConventionIncubatingEventDefinitions` | `RegistryLoader.Signals` | `EventDefinitionsEmitter` | `EventDefinition` fields. |
| Entity definitions | `SemanticConventionEntityDefinitions`, `SemanticConventionIncubatingEntityDefinitions` | `RegistryLoader.Signals` | `EntityDefinitionsEmitter` | `EntityDefinition` fields with describing/identifying attribute references. |

### Enum value sets

Every generated `…Values` class, in the package projection and in both consumer
projections, ends with the vocabulary-owned value set so consumers validate against the
registry instead of retyping it:

```csharp
/// <summary>Every catalogued value, in registry order.</summary>
public static global::System.Collections.Generic.IReadOnlyList<string> AllValues { get; } = new[] { "CONNECT", "DELETE", "GET", /* … */ };
/// <summary>Whether <paramref name="value"/> is a catalogued value (ordinal).</summary>
public static bool Contains(string value) { /* ordinal scan of AllValues */ }
```

`AllValues` follows the constants' order and stability filter of its projection (registry
order in the consumer projections, identifier order in the package projection; deprecated
members are included exactly when their constants are). The initializer holds string
literals, so a deprecated constant never raises CS0618, and it is netstandard2.0-plain
(no LINQ, no collection expressions). The property is `AllValues` rather than `All` because
`cassandra.consistency.level` declares a constant `All`; the emitter fails generation if a
registry member ever collides with `AllValues` or `Contains`.

### Package projections

Three assembly-level markers project a whole registry tier in the layout the compiled
packages ship, under the package root namespace they name. They take no prefix.

| Marker | Emits |
|---|---|
| `[assembly: SemanticConventionAttributesPackage("<root>")]` | `<root>.Attributes.{Root}.{Root}Attributes` (`public static class`) for every registry root with a stable or deprecated row, plus `<root>.SchemaUrl` carrying the pinned schema URL. |
| `[assembly: SemanticConventionIncubatingAttributesPackage("<root>")]` | `<root>.Attributes.{Root}.{Root}Attributes` for every registry root, all stability tiers. |
| `[assembly: SemanticConventionTelemetryNamesPackage("<root>")]` | `<root>.Names.QylTelemetryNames` with the qyl-owned `Scopes` and `Events` constants. Declared by the stable package: the `Qyl.Telemetry` producer packages build their `ActivitySource` and `Meter` from these scope names and may not read the incubating tier. |

Member names in the package layout drop the root segment (`http.route` → `HttpAttributes.Route`),
unlike the contrib-shape class markers (`AttributeHttpRoute`). The projection is exactly how the
`Qyl.Telemetry.SemanticConventions` and `.Incubating` packages are built: each package project
references this generator as an analyzer and declares its markers in `SemanticConventions.cs`;
the generator never references those packages.

```csharp
[assembly: SemanticConventionAttributesPackage("Qyl.Telemetry.SemanticConventions")]
// Qyl.Telemetry.SemanticConventions.Attributes.Http.HttpAttributes.RequestMethod
// Qyl.Telemetry.SemanticConventions.SchemaUrl.Current
```

`PackageProjectionTests` pins the whole projection: five full-file byte-identity snapshots
(`http` stable and incubating, `qyl`, `QylTelemetryNames`, `SchemaUrl`) — with
`QylTelemetryNames` asserted in the stable projection and absent from the incubating one — and
`Snapshots/qyl.package.manifest.sha256`, one `<sha256>  <hint name>` line per file both
projections emit; a mismatch names every differing, missing, and extra file, and only
`REGEN_SNAPSHOTS` rewrites them. Doc comments are rendered from the registry's markdown
into well-formed XML (`PackageDocComments`), and generated docs are compiled in
`DocumentationMode.Diagnose` so a malformed comment fails the gate.

The marker attributes are generated into the consuming assembly via
`RegisterPostInitializationOutput` as `internal`, `[Conditional]` attributes.
The definition types are not generated: they are public types of
`Qyl.Telemetry.SemanticConventions`, so definitions produced in different
assemblies share one type family.

Stable markers emit stable rows plus deprecated migration symbols. Incubating
markers are supersets: stable + development/alpha/beta/release-candidate +
deprecated. This mirrors Java/Python's incubating package behavior and avoids
breaking consumers when conventions are promoted.

Choose one projection per prefix in normal consumer code. Incubating is a
superset, so declaring both stable and incubating activity helpers for the same
prefix in the same namespace can make shared extension methods ambiguous. If a
test fixture intentionally declares both, call the generated static helper
class explicitly.

## Diagnostics

| ID | Severity | When |
|---|---|---|
| `QYLSG001` | Error | A definition marker (`*MetricDefinitions`, `*SpanDefinitions`, `*EventDefinitions`, `*EntityDefinitions`) is declared in a compilation that does not reference `Qyl.Telemetry.SemanticConventions`. |

## Versioning

Tracks two upstream source registries plus the qyl-owned one:

| Source | Canonical pin | Generated exact provenance |
|---|---|---|
| [Core semantic conventions](https://github.com/open-telemetry/semantic-conventions) | [`Version.props`](../../Version.props) | The `core` entry in [`resolved-registry.json`](Resources/resolved-registry.json) |
| [GenAI semantic conventions](https://github.com/open-telemetry/semantic-conventions-genai) | [`Version.props`](../../Version.props) | The `genai` entry in [`resolved-registry.json`](Resources/resolved-registry.json) |
| qyl-owned vocabulary | [`qyl-registry.json`](Resources/qyl-registry.json) | The `qyl` entry in [`resolved-registry.json`](Resources/resolved-registry.json): `source_ref` is the file, `source_commit` is the SHA-256 of its bytes |

The upstream provenance entries carry the exact resolved ref, commit, and schema URL;
they are regenerated from the canonical pins instead of copied into this document. The
`qyl` entry, and every merged qyl row, carries `source_ref: qyl-registry.json` and
`source_commit` = SHA-256 of that file's bytes, with `schema_url` and `source_date_epoch`
as JSON null: qyl publishes no schema URL and has no upstream date.

The GenAI registry is development-stage. It is pinned by commit SHA and must
not be presented as a stable v1.42.0 release.

The embedded registry is regenerated by `scripts/generate.sh` with the Weaver
version pinned in `Version.props`; generation fails on a different binary. The script
runs Weaver twice, once for core and once for GenAI, then `scripts/merge_registries.py`
merges both projections and the qyl-owned registry with a dedup key per row kind (group
id, attribute key, metric name, event name, entity id; the last source wins) while
preserving per-row source metadata. Because the merge is last-wins, it refuses a qyl
attribute outside the `qyl.*` namespace and any qyl attribute, metric, or metric group
that shadows an upstream row, naming the offending key
(`tests/scripts/test_merge_registries.py` covers the guard without Weaver). The core run excludes `gen-ai`, `mcp`, `openai`, and `aws-bedrock`, so those
rows come only from the GenAI source. qyl attributes join the catalog, qyl metrics join
`metrics` and `groups` with their attribute references resolved against the merged
catalog, and the qyl scope and event names land at the root, so `RegistryLoader` and
every other consumer see `qyl.*` with no special casing.

The merge also fingerprints every effective model file, preserves both manifests,
and embeds every referenced GenAI JSON Schema. Two generated consumers share that
same projection:

- `emit_registry_resources.py` publishes the complete resolved registry and raw
  structured-payload schemas through the incubating package.
- `emit_analyzer_registry.py` derives attribute types, enum spellings, GenAI/MCP
  span requirements, provider refinements, and metric names for the analyzer project.

Both scripts support `--check`; normal repository builds and CI fail when their
committed outputs drift from `resolved-registry.json`. `emit_typespec_keys.py`
projects the upstream keys for qyl-api-schema and deliberately excludes the qyl rows
(qyl's public contracts change owner-first, not as a side effect of the merge); it
writes no file in this repository, so it has no `--check` mode.

The generated member shape is snapshot-tested per stability tier on the repository's
`net10.0` test host. The release gate also restores the packed source generator into
a clean `net10.0` consumer, compiles generated members, and executes the result.

Licensed under Apache-2.0. Generated content is derived from the
Apache-2.0 OpenTelemetry semantic-conventions registries.
