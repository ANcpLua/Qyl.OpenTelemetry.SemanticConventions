# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish workflow stamps the
published version from the `v*` tag that triggers it. That tag runs the release gate: CI packs the solution, publishes
through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a clean `net10.0`
consumer.

## [9.5.0] - 2026-10-10

### Changed

- **The genai pin moves from `fee465d` to `6fd0d76`**, the head of
  `open-telemetry/semantic-conventions-genai` `main` on 2026-10-09, which closes
  [#51](https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions/issues/51). 25 upstream
  commits; six files under `model/gen-ai/` changed. The core pin stays at `v1.44.0` and Weaver at
  `0.27.0`. The stable package changes only in its provenance line, because every GenAI
  convention is at `development` stability; the surface below is the Incubating package and the
  analyzer facts.
  - **The one token-usage histogram is gone.** Upstream
    [genai#374](https://github.com/open-telemetry/semantic-conventions-genai/pull/374) replaces `gen_ai.client.token.usage` with five counters,
    `gen_ai.client.inference.usage.{input_tokens,output_tokens,cache_read.input_tokens,cache_write.input_tokens,reasoning.output_tokens}`,
    and two per-operation histograms,
    `gen_ai.client.inference.operation.{input_tokens,output_tokens}`. Input and output are in
    the metric name now, and `gen_ai.token.type` (`input`, `output`) is replaced in place by
    `gen_ai.token.modality` (`text`, `image`, `audio`, `unknown`). Upstream renamed rather than
    deprecated, so `GenAiIncubatingMetricDefinitions.GenAiClientTokenUsage`,
    `GenAiAttributes.TokenType`, `TokenTypeValues` and `SetGenAiTokenType` go without a deprecated
    alias, and `AttributeMapping` carries no rename for `gen_ai.token.type`.
  - **Inference signals live under `gen_ai.client.inference`**
    ([genai#521](https://github.com/open-telemetry/semantic-conventions-genai/pull/521)): the span `gen_ai.inference.client` is
    `gen_ai.client.inference` (`GenAiIncubatingSpanDefinitions.GenAiClientInference`), the two
    streaming histograms are `gen_ai.client.inference.time_to_first_chunk` and
    `.time_per_output_chunk`, and the new `gen_ai.client.inference.duration` takes the inference
    case over from `gen_ai.client.operation.duration`, which stays for the other operations. The
    provider refinements follow (`openai.gen_ai.client.inference` and so on), so the span rules
    QYL0401 resolves against carry the new ids; their discriminators and required attributes are
    unchanged.
  - **New vocabulary.** `gen_ai.main_agent.{id,name,description}` and the `gen_ai.main_agent`
    entity ([genai#270](https://github.com/open-telemetry/semantic-conventions-genai/pull/270)), the first GenAI entity, so
    `GenAiIncubatingEntityDefinitions` is a new file; `gen_ai.skill.{name,description,source.uri,resource.name}`
    ([genai#498](https://github.com/open-telemetry/semantic-conventions-genai/pull/498)) with three `execute_tool` refinements for loading a skill,
    reading a skill resource and running a command; `gen_ai.conversation.id` is conditionally
    required on the `execute_tool` span ([genai#518](https://github.com/open-telemetry/semantic-conventions-genai/pull/518)); and the
    `gen_ai.client.inference.operation.details` event is recommended rather than opt-in and SHOULD
    be recorded at DEBUG ([genai#579](https://github.com/open-telemetry/semantic-conventions-genai/pull/579), [genai#520](https://github.com/open-telemetry/semantic-conventions-genai/pull/520)). The
    other upstream commits in the range change notes, lock files and tooling only.

- **QYL0402 names the registry's token histograms instead of one metric.**
  `registry/policies-v2/genai_invariants.rego` demanded exactly one GenAI metric named
  `*.token.usage`, and `SemconvRegistryFacts.GenAiTokenUsageMetricName` was that metric; the new
  registry has none, so both had nothing to point at. The policy now asks for at least one GenAI
  histogram whose unit is `{token}` (the unit, not the name: `gen_ai.server.time_per_output_token`
  carries the word and measures seconds), and the facts carry them as `GenAiTokenHistogramNames`.
  The rule fires exactly as before, on a `[Histogram]` named `gen_ai.*token*` that the registry
  does not define; its message now ends "its token histograms are
  'gen_ai.client.inference.operation.input_tokens', 'gen_ai.client.inference.operation.output_tokens'".

- **`VersionPrefix` and the registry's schema URL move to 9.5.0.** `AttributeMapping.QylSchemaUrl`
  and every generated header name `https://qyl.at/schemas/9.5.0`, and qyl.at serves that document.

- **`scripts/check-pin-freshness.py` counts a genai commit only when it changes `model/`.** The
  manifest pins semantic-conventions-genai's `model/` sub-folder and nothing else, yet the check
  counted every commit on `main`: of the 25 since `fee465d`, 19 touched lock files, reference
  implementations or tooling and could not have changed a generated byte, and a repository with no
  releases would have reopened #51 at the next such commit. The compare's file list decides now: no
  file under `model/` changed means current, with the distance named; otherwise the changed
  `model/` files are listed above the commits. A file list the API may have truncated (300
  entries), or none at all, is "unknown" rather than "current", as every other unprovable lookup.

- **The shared payload detection builds its local-flow set once per method.** Every rule that
  asks "does this dictionary or `TagList` reach telemetry?" (QYL0005, QYL0009/QYL0010, and now
  QYL0202 and QYL0601) goes through `TelemetryAttributePayloadDetection`, which answered by
  walking the whole enclosing operation tree once per literal. It now registers per operation
  block, computes the locals that flow into a sink once and lazily, and each literal answers
  with a lookup. Each payload also carries *which* sink it reaches (`TelemetryPayloadSink`), so
  a rule can ask for metric measurements alone. A plain array local handed to
  `StartActivity(tags:)` is now covered too; before, only a collection expression was.

- **QYL0601 finds the sink semantically and matches sensitive words on word boundaries.** The
  rule fired wherever a string literal sat under a call whose method or receiver *name*
  contained "tag", "attribute", "span" or "activity", and matched sensitive words as substrings,
  so `tokens` matched `token` and `pipeline` matched `pin`. Against the pinned registry's own
  1031 attribute keys, that flagged 33, every GenAI token-usage counter among them. The rule now
  runs on the shared payload detection, so it fires only where a key provably reaches a tag
  setter, baggage, a metric, `StartActivity` tags, a logger scope, resource attributes, an event
  or a link; sensitive words match on segment boundaries, so `api_key`, `api.key`, `apiKey` and
  `x-api-key` all read as `api key` and `input_tokens` no longer reads as `token`; and a key the
  registry defines outright is exempt, since the registry already decided that
  `aws.secretsmanager.secret.arn` is safe to emit. A key that only extends a registry template,
  such as `http.request.header.authorization`, is still flagged. Of the registry's own keys,
  none is flagged now.

- **QYL0202 covers the SDK path, not only `[Tag]` parameters.** A high-cardinality key in the
  tags of `Counter.Add`, `Histogram.Record`, `UpDownCounter.Add` or a `Measurement`, or in a
  `TagList` built in a local and passed to one of those, now reports; the same key on a span
  does not. The word list matches on segment boundaries like QYL0601's.

- **QYL0006 and QYL0703 fire on a receiver typed as the builder itself, and QYL0006 accepts a
  schema URL that arrives as a constant.** `BuilderCallDetection` accepted a receiver that
  *inherits from* or *implements* a builder type but not one that *is* the type, and the
  `builder` parameter of `WithTracing(builder => ...)` is `TracerProviderBuilder` itself, so
  neither rule fired on the common shape. QYL0006 also recognized a schema URL only as a literal
  or a method name containing "schema"; the generated `SchemaUrl.Current`, or a consumer's own
  `const`, referenced by name was a false positive. It now resolves constant values through the
  semantic model.

- `SemconvRegistryFacts.IsRegistryAttributeKey` is generated beside `IsKnownAttributeKey`: true
  only for a key the registry defines outright, not for one that extends a template.

- `WeaverVersion` moves from `0.26.1` to `0.27.0`. The regenerated output differs from the
  `0.26.1` one only in the recorded Weaver version. Weaver 0.27.0 extends a template only
  across a dot, so `http.request.headers.host` is no longer an instance of
  `http.request.header`: the 0.27.0 binary raises a `missing_attribute` violation on it, and
  `otel.rego`, replaced with the `v0.27.0` copy, raises `extends_namespace` (information).
  `scripts/check-live-check-policies.sh` pins both on a new `template_boundary` span, which
  fails under 0.26.1; the other pinned findings are unchanged. A consumer still on Weaver
  0.26.1 runs this policy set with a binary that matches by prefix, so it draws the
  `extends_namespace` advice but not the `missing_attribute` violation.

### Upstream watch

- **Libraries still emit the names this registry no longer knows.** Microsoft.Extensions.AI 10.9.0
  and Microsoft.Agents.AI 1.20.0, the versions `Qyl.OpenTelemetry.AutoInstrumentation` pins, record
  `gen_ai.client.token.usage` with `gen_ai.token.type`, and its GenAI demo asserts exactly that.
  Against this registry those are an unknown metric and an unknown key: Weaver's live-check reports
  `missing_attribute` as a violation, and the qyl collector counts and drops an unknown key at
  ingest. The registry's mechanism for a key a pinned library writes and upstream does not define
  is a `registry/vendor/` file; none is added in this release, so a consumer moving to 9.5.0 decides
  that first.

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

## [9.4.0] - 2026-09-11

### Changed

- **The registry's schema URL is versioned with the package, and it resolves.**
  `registry/manifest.yaml` named `https://qyl.at/schemas/9.3.0` by hand; the constant
  `AttributeMapping.QylSchemaUrl` and every generated file header carried it, and qyl.at answered
  404. The manifest now names `https://qyl.at/schemas/9.4.0`, `scripts/generate.sh` refuses to
  generate when the URL and `VersionPrefix` disagree, and qyl.at serves an OpenTelemetry schema
  file at every version it ever named (9.3.0, 9.3.1, 9.4.0). Generated code is regenerated from
  the same registry; no attribute, metric, span, event or entity changed.
- The README's install lines no longer pin a version, and the live-check walkthrough reads the
  tag from GitHub instead of naming one.

## [9.3.1] - 2026-09-11

### Changed

- **Each package ships its own README.** All three packages carried the whole repository README,
  350 lines of registry, policy, generation and release detail, as their nuget.org readme. Each
  packable project now has a consumer README beside its csproj: what the package is, how to add
  it, the surfaces it exposes, and absolute links into the repository for the rest. The registry
  and the generated code are unchanged.

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

