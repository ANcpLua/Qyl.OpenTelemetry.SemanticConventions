# Changelog archive

Entries older than the newest three, which stay in [CHANGELOG.md](CHANGELOG.md).

## [9.9.1] - 2026-10-12

- Run analyzer documentation checks from the completed generator binary; remove nested `dotnet run` builds.
- Define `qyl.gen_ai.tool.call.arguments_hash` once in the shared registry for producer-supplied, opaque argument fingerprints.
- Generate matching C# and TypeScript constants; no raw argument capture or instrumentation behavior changes.

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).

## [9.9.0] - 2026-10-12

- Generate stable and incubating TypeScript bindings from the same resolved registry as C#.
- Add the dependency-free `@ancplua/qyl-semantic-conventions` package and packed-consumer checks.
- Publish all four artifacts at one version, require their exact names, and verify installed public consumers.
- Make the shared registry fingerprint independent of local source paths and prove full packed TypeScript/C# vocabulary parity.
- Correct C# integer enum constants, value sets, Activity setters and analyzer type facts to preserve registry numeric values, matching TypeScript.
- Declare the existing `qyl.mcp.operation` event in the shared registry.

## [9.8.0] - 2026-10-11

### Added

- Declare all 11 metrics published by Quartz.NET 4.4.0's native `Quartz` meter, with
  their instrument kinds, units, required dimensions, conditional error/execution-group
  dimensions and source citations. The generated incubating metric API and analyzer catalog
  now recognize job execution, trigger misfires/retries/acquisition, cluster recovery/check-in,
  and job-store operation/lock timings. Existing upstream pins and advice policies are unchanged.
- Move the package version and schema URL to 9.8.0. AutoInstrumentation 23.1.0 consumes these
  definitions when enabling Quartz metrics.

### Corrected before publication

- Require native Quartz `error.type` only when Quartz records an exception; a job can report
  failure without throwing. Clarify that self-recovery reports the scheduler's own instance id.

## [9.7.0] - 2026-10-10

### Changed

- **Eight vendor models re-read at the releases `Qyl.OpenTelemetry.AutoInstrumentation` 23.0.0
  pins.** Every file-and-line citation was resolved again at the new tag by content, not by
  number, and the version, ref and briefs moved with it: Grpc.Net.Client 2.84.0, MongoDB.Driver
  3.12.0, Elastic.Transport 1.1.0 and Elastic.Clients.Elasticsearch 9.5.3 (all citations at the
  same lines), Azure.Core 1.63.0 (one citation moved), MassTransit 8.5.11 (no tag in the
  repository yet, so the ref is the commit the package's nuspec names) and ODP.NET 23.26.301.
  - **Quartz.NET 4.4.0** moved 30 citations and added three keys the library now writes:
    `quartz.job.result` on the job-execution span and on `quartz.job.execution.duration`,
    `quartz.jobstore.lock` on the new `quartz.jobstore.lock.wait.duration` histogram, and
    `quartz.jobstore.trigger.fired_on_acquire` on the acquisition span. The two citations the
    content match could not place, `quartz.jobstore.trigger.count` and `quartz.jobstore.batch.size`
    in `TracingJobStore.cs`, were re-read by hand; both tags are set at more sites than 4.0.0 had.
  - **Two vendor files were wrong before this release.** `mysql-data.yaml` said MySql.Data 9.7.0
    while the instrumentation pins 26.7.0: Oracle renumbered Connector/NET, the 26.7.0 assembly
    says so, its nuspec names no repository, and the newest source tag is 9.7.0, which is where
    the `connector-net` source name was read and which the 26.7.0 assembly still carries. The
    file says 26.7.0 at ref 9.7.0 and explains why. `oracle-data-provider-for-net-odp-net-core.yaml`
    said 23.4.0 while the pin was 23.26.300; it says 23.26.301. And
    `elastic-clients-elasticsearch.yaml` cited Elastic.Transport source paths as if they were in
    the Elasticsearch client repository; they are named as elastic-transport-net 1.1.0 paths now.
  - `azure-core.yaml` describes Azure.Core as a direct pin at 1.63.0 beside Azure.Storage.Blobs
    12.30.1, which is how the instrumentation pins it from 23.0.0 on.

- **`VersionPrefix` and the registry's schema URL move to 9.7.0**, and qyl.at serves
  `https://qyl.at/schemas/9.7.0`. The stable package changes only in its provenance line.

## [9.6.0] - 2026-10-10

### Added

- **Three vendor models for the Microsoft GenAI stack, read at the versions
  `Qyl.OpenTelemetry.AutoInstrumentation` 22.0.0 pins.** 9.5.0 followed upstream in dropping
  `gen_ai.client.token.usage` and `gen_ai.token.type`, and no library has followed yet:
  Microsoft.Extensions.AI 10.10.0 still records that histogram with that dimension, and
  Microsoft.Agents.AI 1.24.0 emits through the same `OpenTelemetryChatClient` under its own source
  name. Without a declaration the collector counts and drops the key and QYL0201 rejects the
  metric name. The registry's answer to a key a pinned library writes and upstream does not define
  is a vendor file, so there are three, each with the file and line every finding was read at:
  - [`registry/vendor/microsoft-extensions-ai.yaml`](registry/vendor/microsoft-extensions-ai.yaml)
    declares `gen_ai.token.type` with the six values the library writes (`input`, `output`, and
    the realtime session's `input_audio`, `input_text`, `output_audio`, `output_text`), the four
    realtime span counts `gen_ai.usage.{input_audio,input_text,output_audio,output_text}_tokens`
    that upstream spells `gen_ai.usage.audio.input_tokens` and so on, and the histogram
    `gen_ai.client.token.usage` itself as a `metric.vendor.*` group with the dimensions upstream gave
    it. `GenAiAttributes.TokenType`, `TokenTypeValues`, `SetGenAiTokenType` and
    `GenAiIncubatingMetricDefinitions.GenAiClientTokenUsage` are back, now as vendor rows, so a
    consumer that compiled against 9.4.0 compiles against 9.6.0.
  - [`registry/vendor/microsoft-agents-ai.yaml`](registry/vendor/microsoft-agents-ai.yaml) is
    metadata only: the Agent Framework declares no key of its own, and the file puts
    `Experimental.Microsoft.Agents.AI` into `QylTelemetryNames.VendorActivitySources` and QYL0200's
    allowlist with the lines that prove it is Microsoft.Extensions.AI's client under another name.
  - [`registry/vendor/microsoft-agents-ai-workflows.yaml`](registry/vendor/microsoft-agents-ai-workflows.yaml)
    declares the sixteen keys the workflow runtime puts on `workflow.build`, `workflow.session`,
    `workflow_invoke`, `executor.process`, `message.send` and `edge_group.process`:
    `workflow.{id,name,description,definition}`, `build.error.{message,type}`,
    `executor.{id,type,input,output}`, `message.{content,source_id,target_id}` and
    `edge_group.{type,delivered,delivery_status}`. Until now the collector dropped every one of
    them, workflow id included. `build`, `edge_group`, `executor` and `workflow` join
    `qyl.attribute.namespace`; `message` and `session` were already members. Four keys in the
    library's `Tags.cs` are not declared because upstream already owns them: `error.type`,
    `error.message` and `session.id` travel as upstream keys, and `message.type` is upstream's
    RPC message-type key, deprecated in 1.44.0 as obsoleted with no replacement, so
    `AttributeMapping.IsObsoleted` is true for it and the collector drops the workflow's .NET
    type name at ingest. That collision is the library's; the fix is a library-owned prefix
    upstream.
  - `Experimental.Microsoft.Extensions.AI`, `Experimental.Microsoft.Agents.AI` and
    `Microsoft.Agents.AI.Workflows` are `VendorActivitySources` members, and `AttributeMapping.IsVendorPassThrough`
    is true for all twenty-one keys.

### Changed

- **QYL0402 judges against the complete metric inventory.** The rule compared a `gen_ai.*token*`
  histogram name against the GenAI metrics of upstream provenance only, so the vendor-declared
  `gen_ai.client.token.usage` would have been reported as unknown while QYL0201 accepted it. It
  now asks `SemconvRegistryFacts.IsKnownMetricName`, the set every other metric rule reads; the
  upstream-only set and `IsKnownGenAiMetric` had no other reader and are gone.

- **`VersionPrefix` and the registry's schema URL move to 9.6.0**, and qyl.at serves
  `https://qyl.at/schemas/9.6.0`.

- **Renovate no longer tracks the genai branch commit.** Its `git-refs` manager opened a branch
  that moved the digest in `registry/manifest.yaml` and nothing else, which `check-generated.sh`
  fails by definition; the branch sat under "pending status checks" since the pin was set. The
  pin-freshness report is where that movement is seen, and the bump is a hand-made commit that
  regenerates. The semantic-conventions release and the Weaver release stay with Renovate.

- A correction to the 9.5.0 "Upstream watch" note below: `Qyl.OpenTelemetry.AutoInstrumentation`'s
  live-check gate judges the nine container-backed demo lanes and not the GenAI one, so 9.5.0 did
  not fail that gate; the consequence it did have was the collector dropping `gen_ai.token.type`
  and the compile error on `GenAiAttributes.TokenType`, both of which this release removes.

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

