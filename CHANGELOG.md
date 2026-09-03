# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish
workflow stamps the published version from the `v*` tag that triggers it. That tag runs the release
gate: CI packs the solution, publishes through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a
clean `net10.0` consumer.

## [Unreleased]

### Changed

- `SemConvGenAiRef` moves from `eaefa14` to `3bda576`, the head of
  open-telemetry/semantic-conventions-genai `main`, absorbing eleven upstream commits. The core
  schema stays `1.44.0` and Weaver stays `0.25.1`: the pinned GenAI manifest still declares
  `1.44.0` as its dependency, so `generate.sh`'s core-dependency guard passes without a coupled
  bump. The registry was regenerated and is idempotent on a second run. **Nothing was added,
  removed, or renamed** — 980 catalog attributes, 1287 groups, 64 entities before and after, and
  no generated constant disappeared, so no published package's public API moves. Three substantive
  deltas, everything else in the regenerated `resolved-registry.json` being per-row pin
  provenance:
  - `gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_write.input_tokens` are no
    longer referenced by the `gen_ai.invoke_agent.internal` span (upstream #469). Both were
    `recommended` there, never required; both attributes stay in the catalog, stay `development`,
    stay undeprecated, and stay on the inference spans, so `SemconvRegistryFacts.g.cs` and
    `QYL0401` are unaffected.
  - The per-message `finish_reason` field of the `gen_ai.output.messages` payload schema is
    deprecated in favour of `gen_ai.response.finish_reasons` (upstream #363). In the shipped
    `schemas/gen-ai/gen-ai-output-messages.json` it leaves the `OutputMessage` `required` list and
    gains `"deprecated": true`, a `null` type member, and a `null` default — a relaxation, so a
    document that omits it now validates and one that carries it still does.
  - `gen_ai.response.finish_reasons` gains a note pinning its contract (one entry per returned
    generation, in order; `error` for a position whose reason never arrived) and a third example.
    Its type, stability, and brief are unchanged.

  Across all 136 files the package projections emit, the only changes are the provenance header sha
  in `GenAiAttributes.g.cs`, `McpAttributes.g.cs` and `OpenaiAttributes.g.cs`, and the two doc
  comments above. Every member name is identical, and the stable tier does not move at all.

  The delta is recorded in [`qyl-references/REFERENCE-STATUS.md`](qyl-references/REFERENCE-STATUS.md),
  which this change also introduces — the path
  [`check_pin_freshness.py`](src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/check_pin_freshness.py)
  has always named in its stale-pin report, but which no commit had created.

## [7.1.1] - 2026-09-02

### Fixed

- `Qyl.Telemetry.SemanticConventions.Analyzers` no longer ships `ANcpLua.Roslyn.Utilities.dll`
  beside its own assembly. The compiler loads every analyzer package's dependencies by assembly
  name, so a consumer that also referenced another analyzer package carrying a different build of
  that assembly (`ANcpLua.Analyzers` 2.1.2 carries 2.2.44 against this package's 2.2.41) could not
  create a single rule: every analyzer failed with CS8032, an error under
  `TreatWarningsAsErrors`. The utilities are now compiled in from
  `ANcpLua.Roslyn.Utilities.Sources`, the way the source generator already does, so the package
  has no runtime dependency to collide. As a consequence the analyzer classes are `internal`
  (their base type is now an internal source-included type); nothing outside this repository
  referenced them, and Roslyn discovers analyzers by attribute, not by visibility.
- `QYL0101` honours the `OtelSemConvInstrumentationLibrary` opt-out introduced in 7.1.0. An
  instrumentation library declares its `ActivitySource`s for a separate hosting package to
  register, so the `AddSource()` call the rule looks for is never in the library's own
  compilation and the report was a false positive there.

## [7.1.0] - 2026-09-02

### Added

- `qyl-registry.json` gains `local_attribute_values`: qyl-local members appended to an *upstream*
  open enum, the only sanctioned way a qyl row touches an upstream row. `messaging.system` declares
  `masstransit` and `nservicebus`, each noted as local to qyl and absent from upstream OpenTelemetry
  semantic conventions, so the registry-derived projections stop treating them as unknown values.
  The merge fails naming the value the moment an upstream bump lands it, so the local declaration is
  deleted rather than shadowing upstream.
- [`UPSTREAM-dotnet_wcf.md`](UPSTREAM-dotnet_wcf.md): a draft issue for
  open-telemetry/semantic-conventions asking where `dotnet_wcf` belongs now that `rpc.system` is
  renamed to `rpc.system.name`, which declares no WCF member. Not filed; `dotnet_wcf` is unchanged
  in this repo.
- `OtelSemConvInstrumentationLibrary` — a per-project MSBuild opt-out for `QYL0008`. An
  instrumentation library that deliberately version-locks with the incubating tier sets
  `<OtelSemConvInstrumentationLibrary>true</OtelSemConvInstrumentationLibrary>` and the rule
  stops reporting in that project; every project that leaves it unset is unaffected. The
  property is exposed to Roslyn through the package's `buildTransitive` props, alongside the
  existing `PublishAot` and `EventSourceSupport` entries.

### Changed

- `QYL0008` recognises every local-copy form of the mitigation it recommends, not only a
  `const` field. A `private static readonly string` copy, a `private static readonly string[]`
  table of copies, and a method-local `const string` copy now suppress the diagnostic the same
  way, so a library that follows the documented advice is no longer warned for doing it in the
  shape its own code calls for. A direct incubating reference in any other position still
  reports.

## [7.0.0] - 2026-09-02

### Changed

- **BREAKING:** the qyl-owned telemetry scope names follow the `Qyl.Telemetry` package family.
  `qyl-registry.json` renames `Qyl.OpenTelemetry.AutoInstrumentation`,
  `Qyl.OpenTelemetry.AutoInstrumentation.Database` and
  `Qyl.OpenTelemetry.AutoInstrumentation.NServiceBus` to `Qyl.Telemetry.AutoInstrumentation`,
  `Qyl.Telemetry.AutoInstrumentation.Database` and `Qyl.Telemetry.AutoInstrumentation.NServiceBus`.
  The producer packages have been `Qyl.Telemetry.AutoInstrumentation*` since their `9.0.0`, so the
  scope name a producer constructs and the name `QYL0200` accepts had drifted apart. Every
  downstream projection moves with it: `QylTelemetryNames.Scopes.QylOpenTelemetryAutoInstrumentation*`
  become `QylTelemetryNames.Scopes.QylTelemetryAutoInstrumentation*`, and the `QYL0200` allowlist
  (`SemconvRegistryFacts.KnownScopeNames`) no longer accepts the old spellings. Pairs with
  AutoInstrumentation `10.0.0`, which consumes them through `QylTelemetryNames.Scopes`.
- **BREAKING:** `QylTelemetryNames` ships from the stable `Qyl.Telemetry.SemanticConventions`
  package as `Qyl.Telemetry.SemanticConventions.Names.QylTelemetryNames`, not from `.Incubating`.
  The class carries the `ActivitySource` and `Meter` scope names the `Qyl.Telemetry` producer
  packages construct their instrumentation with, and qyl's architecture forbids those packages from
  reading the incubating tier — so the only way to consume them was a reference the architecture
  disallows. The names are qyl-owned rather than upstream, so nothing about their content was
  incubating; only their address was. The attribute constants are unaffected: every `qyl.*` row is
  development-stability and stays incubating-only.
- `Qyl.Telemetry.SemanticConventions.Analyzers` is a released package rather than a preview-only
  one. The `PackPreviewAnalyzers` gate and the `_RequirePreviewAnalyzerVersion` target that
  rejected a stable `PackageVersion` are gone, so `dotnet pack` on the solution now produces four
  `.nupkg` files. `eng/release/verify-packages.sh` counts, unpacks and restores all four — it
  asserts the analyzer package carries `analyzers/dotnet/cs/`, its `buildTransitive` props and the
  three generated editorconfig severity profiles, and that it installs into the release smoke
  consumer beside the other three. `nuget-publish.yml` needed no change: it is the canonical fleet
  template and already pushes every packed `.nupkg`.

### Added

- The generator README documents that a free-form string attribute has no generated `…Values`
  class and will not get one, using `messaging.operation.name` (free-form, system-specific)
  against `messaging.operation.type` (the enum) as the worked case — an authority instrumentation
  can cite instead of treating a deprecated *operation type* member as a constraint on an
  *operation name*.
