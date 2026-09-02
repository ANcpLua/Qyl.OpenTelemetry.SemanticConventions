# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish
workflow stamps the published version from the `v*` tag that triggers it. That tag runs the release
gate: CI packs the solution, publishes through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a
clean `net10.0` consumer.

## [Unreleased]

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
