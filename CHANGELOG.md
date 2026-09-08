# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish workflow stamps the
published version from the `v*` tag that triggers it. That tag runs the release gate: CI packs the solution, publishes
through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a clean `net10.0`
consumer.

## Unreleased

### Upstream watch

- **A second policy set now judges this registry's telemetry from outside it.**
  `ANcpLua/opentelemetry-compliance-checker` carries `policies/live/{upstream,contract,regression}.rego`
  and `policies/compatibility/stable.rego`, overlapping in purpose with this registry's
  [`registry/policies/live_check_advice/`](registry/policies/live_check_advice) (`otel.rego`,
  `qyl_advice.rego`). The agreed direction is to merge them with this registry as the owner, so
  that one place decides what a conforming span looks like.

  The hazard to carry into that merge, verified 2026-09-08 by running
  `tools/verify-live-check.py` from the AutoInstrumentation repo against this checkout: Weaver's
  live-check needs **both** `--config <registry>/.weaver.toml` **and** `--advice-policies
  <registry>/policies/live_check_advice`. The `.toml` is what switches Weaver's compiled-in
  advisors off, by finding id; Rego alone changes nothing. Moving the policies without the `.toml`
  silently restores every built-in advisor the registry had deliberately suppressed. The same run
  also showed that the verifier aborts before checking anything unless `QYL_SEMCONV_REGISTRY`
  points at this checkout — an absent variable reads as a failure, not as a skip.

- **`--include-unreferenced` is deprecated in Weaver 0.26.1.** Every `weaver registry generate`
  and `weaver registry live-check` run against this registry prints `⚠ The flag
  include_unreferenced is deprecated. Please prefer manually adding the required imports to your
  schema files in the future.` The replacement Weaver points at is the `imports` block in a semconv file, and [
  `schemas/semconv.schema.json`](https://github.com/open-telemetry/weaver/blob/main/schemas/semconv.schema.json)
  gives it exactly three keys — `metrics`, `events`, `entities` — with `additionalProperties:
  false`. Attributes and spans cannot be imported, and this registry generates every attribute key its core and genai
  dependencies carry, so the flag has no replacement yet. The registry is unchanged; revisit when `imports` gains
  attributes or when Weaver removes the flag.

## [9.3.0] - 2026-09-07

Two findings from the `Qyl.OpenTelemetry.AutoInstrumentation` 15.0.0 work, which runs the instrumentation against real
containers on .NET 10.

### Added

- **[`registry/vendor/grpc-net-client.yaml`](registry/vendor/grpc-net-client.yaml)** — the Grpc.Net.Client model, read
  at `v2.83.0`, the version `Grpc.Net.Client` is pinned to. The client owns the `Grpc.Net.Client` `ActivitySource` and
  writes one client span per call, named `Grpc.Net.Client.GrpcOut`, and it puts two keys on that span that upstream
  semantic conventions do not define:
  - `grpc.method` — the full method path, `/package.Service/Method`.
  - `grpc.status_code` — the gRPC status code as its *decimal number* in a string (`status.StatusCode.ToString("D")`),
    not the enum member name. Upstream's
    `rpc.grpc.status_code` is a separate key the client never writes; the collector passes both of these through rather
    than rewriting them.

  The source name is generated as `Names.QylTelemetryNames.VendorActivitySources.GrpcNetClient`, so `AddSource` and a
  span processor's source match need no literal. `grpc` joins
  `qyl.attribute.namespace`, the closed value set the dropped-attribute counter is broken down by, and both keys join
  `AttributeMapping.IsVendorPassThrough`.

### Removed

- **The `qyl.http.client` event** from [`registry/qyl/events.yaml`](registry/qyl/events.yaml). Nothing ever emitted it:
  it was declared alongside `qyl.rpc.grpc` and its only reader was a branch in `Qyl.OpenTelemetry.AutoInstrumentation`,
  which 15.0.0 deletes — the HTTP client path carries upstream's `http.client.*` span and needs no qyl-owned event.
  Removing it drops
  `Names.QylTelemetryNames.Events.QylHttpClient` and
  `Incubating.Events.QylIncubatingEventDefinitions.QylHttpClient`, and takes the name out of
  `SemconvRegistryFacts.KnownEventNames`, so QYL0200 now flags it. `qyl.rpc.grpc` is the one event the registry owns.

