# Qyl.Telemetry.SemanticConventions engineering contract

This is the repository's only editable agent/contributor instruction file.
`CLAUDE.md` is a symlink to it. Keep package guidance in the root or packaged
README, released history in releases, and generated analyzer documentation under
`docs/`. Do not add mission prompts, progress diaries, or a second rules file.

## Verified concern status

| Concern | Status | Evidence / concrete remainder |
| --- | --- | --- |
| [`QYL-TELEMETRY-RUNTIME`](https://github.com/ANcpLua/dedupe-28th-july/blob/main/concerns/06-telemetry-vocabulary-and-aot.md) | `IN_PROGRESS` | Package/namespace generation, culture-invariant wire semantics, localized analyzer diagnostics, and generated/AOT gates are tracked. The coordinated G5 producer scope/meter rename, registry regeneration/publication, qyl repin, and consumer/conformance evidence remain; old `scope_names` are current. |

## 1.0.0 target name

This repository ships **`Qyl.Telemetry.SemanticConventions`** and
**`Qyl.Telemetry.SemanticConventions.Incubating`**, plus
`Qyl.Telemetry.SemanticConventions.SourceGeneration` and the preview-only
`Qyl.Telemetry.SemanticConventions.Analyzers`. The rename off
`Qyl.OpenTelemetry.SemanticConventions(.*)` has landed, generated namespace
included — that namespace is consumer-compiled ABI, so it ships as the **birth
ABI of the new package IDs** (architecture §6.2) rather than as a break to a
published one. The old IDs stay frozen on nuget.org at 4.0.0 until they are
unlisted; this repository does not build them.

The new family is **`1.0.0` stable**. Publishing stays tag-triggered — the
committed version is a development fallback and the version tag is the human
act that publishes. Published artifacts are immutable.

The full ledger and the boundary law live in `qyl/ARCHITECTURE-1.0.0.md` — that
document is normative and this one does not restate it. The GitHub repository
keeps its `Qyl.OpenTelemetry.SemanticConventions` name, so `RepositoryUrl` and
every analyzer `HelpLinkUri` still carry it; those are addresses, not package
identity, and they must stay truthful to the repo that actually exists.

Why this package matters more than the rename suggests: it is the **only
artifact both planes share**. The producer-side constants and the collector's
`CollectorSemanticAttributeCatalog.g.cs` are generated from the *same
registry*, which is what makes it structurally impossible for qyl to emit a
name its own collector does not know.

That guarantee holds **only at identical registry versions**. It is not a
property of the design; it is a property of building both sides together. Once
a consumer pins an SDK version and the deployed collector moves on, the
guarantee becomes a skew question. Every changed publication explicitly defines
and tests its supported producer/registry/collector version span and behavior
for unknown attributes.

This package knows nothing about `Activity`, DI, or OTLP, and must not learn.

## Purpose and boundaries

This repository turns pinned OpenTelemetry semantic-convention registries into
stable/incubating .NET constants, Roslyn source-generation APIs, analyzer rules, and
the TypeSpec key projection consumed by `qyl-api-schema`.

Obsolete surfaces converge directly, and current callers update in the same
change. A changed artifact publishes as a new version; do not add compatibility
shims.

## One owner for every generated surface

Every emitter below lives in
`src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/`.

- `Version.props` owns the core semantic-convention registry and Weaver pins.
- `Resources/resolved-registry.json` is the pinned upstream input;
  `Resources/qyl-registry.json` is the qyl-owned one (`qyl.*` attributes, scope
  names, bridge event names). Both are inputs, never hand-derived outputs.
- `generate.sh` owns the Weaver-resolved registry embedded by the packages, and
  chains `emit_analyzer_registry.py` + `emit_registry_resources.py`.
- `emit_attributes.py --write` owns the committed stable and incubating constant
  trees from both registry inputs, including the qyl-owned
  `Incubating/Attributes/Qyl/QylAttributes.g.cs` and
  `Incubating/Names/QylTelemetryNames.g.cs`. One command, all of it;
  `VerifyAttributesHash` covers exactly what that command writes.
- `emit_analyzer_registry.py` owns `SemconvRegistryFacts.g.cs` (QYL0200/QYL0201
  allowlists) from the same two registry inputs.
- `emit_registry_resources.py` owns the incubating package's
  `Registry/SemanticConventionRegistry.g.cs` and its embedded GenAI schemas. The
  `LogicalName` prefixes in the incubating csproj are part of that contract —
  they must equal the assembly name the runtime lookup resolves against.
- `emit_typespec_keys.py` owns
  `qyl-api-schema/generated/otel-keys.gen.tsp`; regenerate that sibling projection
  from the same resolved registry after a pin change. It projects the upstream
  catalog only — `qyl.*` names are .NET-side vocabulary and stay out of the
  product contract.
- `DocsGenerator` owns the analyzer index, migration catalog, SARIF/editorconfig
  artifacts, and per-rule HelpLink pages.
- `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` are maintained
  analyzer inputs, not generated prose.

Never patch generated output. Change the registry input, generator, analyzer, or
documentation generator, regenerate, and commit owner plus outputs together.

Stable constants contain stable and deprecated upstream entries. Incubating constants
contain the unstable surface and may break between minor releases. Development GenAI
conventions remain incubating until their upstream stability changes.

## Contract boundaries

This repository owns telemetry vocabulary, not Qyl product API models and not OTLP
wire messages. The TypeSpec key projection supplies names to `qyl-api-schema`; the
schema repository owns every Qyl client-visible request, response, stream event, and
error. Do not move AutoInstrumentation's internal capability catalog into the public
product-contract repository.

A source-generation or analyzer API needs an executable consumer and tests for the
complete contract. Marker-only declarations, unexecuted sample code, or a mock that
only reproduces the expected string are not acceptance evidence.

The analyzer project is preview-only. Qyl does not consume it yet, and only nine of its
48 rules have executable behavior tests. Keep it out of stable packs. An explicit preview
pack requires `PackPreviewAnalyzers=true` and a prerelease package version, and since the
repo version went stable that prerelease part must now be passed explicitly:
`dotnet pack … -p:PackPreviewAnalyzers=true -p:VersionSuffix=preview.N`. Without it
`_RequirePreviewAnalyzerVersion` fails the pack, which is the guard working — a stable
analyzer package is exactly what it exists to prevent. Stable promotion requires Qyl
consumption and executable coverage for every rule.

Two rules are the qyl architecture's G1/G4 enforcers and are held to production
standard now: `QYL0200` (telemetry names in name positions must be members of the
generated catalog) and `QYL0201` (metric descriptor names must be well-formed catalog
members). Their allowlists come from `SemconvRegistryFacts.g.cs`, generated by
`emit_analyzer_registry.py` from `resolved-registry.json` merged with
`qyl-registry.json` — the qyl-owned vocabulary input (qyl.* attributes, scope names,
bridge event names). Never hardcode a name list in an analyzer; extend
`qyl-registry.json` and regenerate.

That same input also generates the consumer-facing side of the loop:
`QylAttributes` and `QylTelemetryNames` in the incubating package. Both faces —
what the analyzer accepts and what a consumer can reference — come from one file,
so a `qyl.*` name cannot exist as a literal that the catalog does not know.
Producers referencing these constants instead of literals is what makes the loop
closed rather than merely checked.

`scope_names` and `event_names` mirror producer emissions, not package,
assembly, or namespace identity. An emitted-name change is one coordinated G5
slice: update the producer and real evidence first, then the registry and all
generated artifacts, publish/index this package, repin qyl, and prove consumer
conformance. Never move the registry ahead of the wire or hand-edit its outputs.

## MCP wire concepts and the qyl.mcp.* staging namespace

This repository owns the telemetry vocabulary, so undefined MCP wire concepts are
governed here. The MCP 2026-07-28 revision adds concepts OpenTelemetry semconv has
not defined.

- Wire concepts absent from the pinned upstream registry — `requestState`, round
  index, `resultType`, `subscriptions/listen` lifetime, cache hints (`ttlMs` /
  `cacheScope`) — are emitted as constants under an experimental `qyl.mcp.*` staging
  namespace, kept out of the stable tree, and deletion-targeted on every registry pin
  bump that lands an upstream equivalent. Never mint an `mcp.*` constant for an
  unratified concept.
- Constants for era, identity, and status carry the 2026-07-28 semantics the emitters
  must record: era is the negotiated protocol version, not envelope presence;
  `clientInfo` / `serverInfo` are display and logging values, not behavior or security
  inputs; status derives from the JSON-RPC and tool outcome, not HTTP status.

## Build and regeneration

Build all projects with warnings treated as errors:

```bash
dotnet build Qyl.Telemetry.SemanticConventions.slnx -c Release
```

Run both Microsoft Testing Platform executables:

```bash
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.Pipeline.Tests -c Release
dotnet run --project tests/Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests -c Release
```

Important generation gates:

```bash
./build.sh VerifyAttributesHash
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/verify_deprecated_catalog.py
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_analyzer_registry.py --check
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_registry_resources.py --check
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_attributes.py --check
```

The last one is the architecture's G9 snapshot gate for the constant trees, the
`qyl.*` projection included. It overlaps `VerifyAttributesHash` deliberately and
they are not redundant: the hash is one aggregate SHA that proves *something*
changed, `--check` prints *which file and which lines*. It also catches an
orphan — a `.g.cs` no registry root produces any more — which a hash of the
files that exist cannot. It runs on every SourceGeneration build, so a stale
tree fails locally, not only in CI.

Analyzer docs and diagnostic-id consistency are enforced automatically on every
analyzer-project build; there is no separate check target.

Regenerate the constant trees (upstream and qyl-owned, both packages) with:

```bash
python3 src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/emit_attributes.py --write
```

Regenerate the byte-identity snapshots by pointing `REGEN_SNAPSHOTS` at the test
project's `Snapshots/` directory and running the source-generation tests; never
edit a snapshot by hand and never relax an assertion so a changed emitter passes.

After an intentional attribute regeneration, reseed the manifest with
`./build.sh SeedAttributesHash`. Regenerate analyzer documentation with
`./build.sh GenerateDocs`; never edit generated rule pages directly.

## Publishing

Publication is GitHub Actions OIDC trusted publishing. Never add a long-lived NuGet
API key or publish locally. The supported release set is the stable constants,
incubating constants, and source generator. Release work builds, tests, packs,
inspects, waits for registry indexing, restores into a clean consumer, executes it,
and only then tags and announces the release. The committed local version is a
development fallback; the release workflow owns the published version.
