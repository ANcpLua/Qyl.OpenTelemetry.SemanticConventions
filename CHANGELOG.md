# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish
workflow stamps the published version from the `v*` tag that triggers it. That tag runs the release
gate: CI packs the solution, publishes through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a
clean `net10.0` consumer.

## Unreleased

### Upstream watch

- **`--include-unreferenced` is deprecated in Weaver 0.26.1.** Every `weaver registry generate`
  and `weaver registry live-check` run against this registry prints `⚠ The flag
  include_unreferenced is deprecated. Please prefer manually adding the required imports to your
  schema files in the future.` The replacement Weaver points at is the `imports` block in a
  semconv file, and [`schemas/semconv.schema.json`](https://github.com/open-telemetry/weaver/blob/main/schemas/semconv.schema.json)
  gives it exactly three keys — `metrics`, `events`, `entities` — with `additionalProperties:
  false`. Attributes and spans cannot be imported, and this registry generates every attribute
  key its core and genai dependencies carry, so the flag has no replacement yet. The registry is
  unchanged; revisit when `imports` gains attributes or when Weaver removes the flag.

## [9.1.0] - 2026-09-07

The first live-check run of `Qyl.OpenTelemetry.AutoInstrumentation` 14.0.0 against the 9.0.0
registry reported 51 violations. Not one of them was on a span qyl writes: every one was on a
span a pinned library emits on its own `ActivitySource`, which is what qyl subscribes to. The
run split three ways, and this release answers each — 24 keys nothing declared, 23
deprecations the collector already handles, and 4 values the collector already coerces. The
other 39 findings, all undocumented `error.type` values, were already `information`; they are
now labelled as what they are.

### Added

- **Three vendor models**, for the seven keys the run reported as
  `missing_attribute`. Each names the library, the exact pinned version, the repository and
  ref the finding was read at, the licence and the `ActivitySource`s, and each attribute
  cites the file and line of the upstream release that sets it.
  - [`registry/vendor/corewcf.yaml`](registry/vendor/corewcf.yaml) — `soap.message_version`,
    `soap.reply_action`, `wcf.channel.path`, `wcf.channel.scheme`, from CoreWCF `v1.9.1`. The
    pin is `CoreWCF.Http` 1.9.1; the instrumentation, the source `CoreWCF.Primitives` and all
    four tags live one assembly down, in `CoreWCF.Primitives`.
  - [`registry/vendor/azure-core.yaml`](registry/vendor/azure-core.yaml) — `az.schema_url`
    and `az.client_request_id`, from Azure.Core `1.55.0`, the version
    `Azure.Storage.Blobs` 12.29.2 restores to. The source name is the `Azure.*` prefix
    pattern, because Azure.Core builds one source per client type and emits the HTTP spans on
    a fixed `Azure.Core.Http`.
  - [`registry/vendor/elastic-clients-elasticsearch.yaml`](registry/vendor/elastic-clients-elasticsearch.yaml)
    — `db.elasticsearch.schema_url`, from Elastic.Clients.Elasticsearch `9.5.1`. The client
    owns no `ActivitySource`: it hands a mutator to Elastic.Transport, which writes the tag
    onto the `Elastic.Transport` Activity. Elastic.Transport 1.0.0 declares the key name and
    only reads it, so the finding is filed against the client.
  - `soap` and `wcf` join `qyl.attribute.namespace`, the closed value set the
    dropped-attribute counter is broken down by.
- **A live-check advice policy set the registry owns**:
  [`registry/policies/live_check_advice`](registry/policies/live_check_advice) and
  [`registry/.weaver.toml`](registry/.weaver.toml). Weaver's default levels are written for a
  registry that owns everything it sees; these say what the qyl collector actually does with
  each finding, and decide it from the registry entry alone — there is no attribute-key list
  anywhere in the set. An undocumented value on an enum carrying `_OTHER` is
  `open_enum_value`/information, because such an enum is open and the value is prescribed; a
  deprecation with reason `renamed` is `deprecated_renamed`/improvement, because
  `TryGetRename` rewrites it; `obsoleted` is `deprecated_obsoleted`/improvement, because
  `IsObsoleted` drops it; a type mismatch whose value parses as the declared type is
  `type_coercible`/improvement, because the collector coerces it. Everything else keeps
  Weaver's level, and Weaver's own default policy is copied into the set verbatim as
  `otel.rego` so its four name and namespace rules survive the replacement.
  [`scripts/check-live-check-policies.sh`](scripts/check-live-check-policies.sh) asserts the
  exact findings per attribute over a sample covering every rule, and CI runs it.
- **`Incubating.Mapping.AttributeMapping.IsObsoleted`** — every key the registry deprecates
  without naming a replacement, so the collector can drop it and count the drop instead of
  forwarding a key upstream removed. `TryGetRename` answered "what does this key become?" but
  not "does it become anything at all?", so an obsoleted key such as `exception.escaped`,
  which NServiceBus emits, was indistinguishable from a live one. The two are disjoint by
  construction and `AttributeMappingTests` proves it against `SemconvDeprecations`.

### Changed

- **`README` documents how to run live-check against a tag of this repository**, verified:
  Weaver's `<url>[sub-folder]` archive syntax does not work here, because
  `registry/manifest.yaml` names the core dependency by the local path
  `.build/core-filtered/model` that only `scripts/fetch-core.sh` materialises, and neither
  `--config` nor `--advice-policies` accepts a URL at all. `--advice-policies` also fails
  silently on a path it cannot read, loading no policies. The recipe is to fetch the tag,
  run `scripts/fetch-core.sh`, and pass local paths for all three.
- **`lib.pascal` treats `*` as a segment separator**, so a vendor `ActivitySource` name that
  is an `AddSource` prefix pattern still yields a C# identifier
  (`VendorActivitySources.Azure = "Azure.*"`).

## [9.0.0] - 2026-09-07

Weaver is the only generator and YAML the only source. The vocabulary moves into one Weaver
registry, Jinja templates emit the C# straight from it, and the output is committed and
diffed in CI. Breaking, one wave: nothing from the old pipeline is kept alive.

### Removed

- **`Qyl.Telemetry.SemanticConventions.SourceGeneration` is retired.** Its last version is
  `8.1.0`. The Roslyn generator, its marker attributes
  (`[SemanticConventionAttributes]`, `[SemanticConventionMetricDefinitions]`, and siblings)
  and the assembly-level package markers are gone. Everything the generator produced on demand
  is now shipped pre-built by the two packages, so a consumer references the package instead of
  declaring a marker. Three packages ship, not four.
- **`Registry.SemanticConventionRegistry` and the embedded registry are gone** from
  `.Incubating`: `OpenResolvedRegistry`, `TryOpenPayloadSchema`, `PayloadSchemaResources`,
  `CoreSchemaUrl` and `GenAiSchemaUrl`, along with the packed
  `registry/resolved-registry.json` and the eight `registry/schemas/gen-ai/*.json` files. No
  consumer read any of them. The invariant that every GenAI `any` attribute carries a JSON
  Schema stays, as a registry policy.
- **`local_attribute_values` are gone.** `messaging.system` loses the qyl-local members
  `masstransit` and `nservicebus`, and `rpc.system.name` loses `dotnet_wcf`; the same three
  values disappear from `SemconvRegistryFacts`'s `EnumValues`. Nothing consumed the first two.
  `dotnet_wcf` is a local literal in `Qyl.Telemetry.AutoInstrumentation`
  (`QylInterceptedWcfClient.cs:16`) and stays one until upstream lands the value.
- **The TypeSpec keys projection (`emit_typespec_keys.py`, `otel-keys.gen.tsp`) is removed;
  the registry reaches non-.NET consumers as a Weaver JSON target when one appears.** It had
  no consumer, and TypeSpec cannot carry deprecation or open enums.
- **The Python pipeline is gone**: `merge_registries.py`, `emit_analyzer_registry.py`,
  `emit_registry_resources.py`, `emit_common.py`, `emit_typespec_keys.py`,
  `verify_deprecated_catalog.py`, `Resources/qyl-registry.json` and
  `Resources/resolved-registry.json`. `check_pin_freshness.py` survives as
  `scripts/check-pin-freshness.py` and reads `registry/manifest.yaml`.

### Added

- **`registry/`** is the vocabulary. `manifest.yaml` names core (a local filtered copy of
  `open-telemetry/semantic-conventions@v1.44.0`) and genai (a git URL pinned to
  `fee465db`) as dependencies; `qyl/` carries the 15 `qyl.*` attributes, the two qyl-owned
  instruments, the two qyl-owned event names and the six scope names; `vendor/` carries one
  file per pinned third-party library. The pins live in exactly one place: `manifest.yaml`.
  `Version.props` keeps only `WeaverVersion`, and `generated/pins.props` carries the registry
  pins into MSBuild.
- **`templates/`** is the generator: `registry/csharp` emits the C# surface.
  `scripts/generate.sh` runs Weaver;
  `scripts/check-generated.sh` regenerates and fails on `git diff`, and CI runs it.
- **Pre-generated definitions and setter extensions in both packages.** Each package's
  `Generated/` directory holds one file per registry root for six kinds — `Attributes`,
  `Activities`, `Metrics`, `Spans`, `Events`, `Entities` — where 8.1.0 emitted the last five
  into the consuming assembly at compile time. For those five the packages are disjoint: the
  stable package carries the stable and deprecated rows, the incubating package only the rows
  the stable package does not carry, and a registry root with no non-stable rows gets no
  incubating type. `Attributes` is unchanged — both packages carry it in full, because the
  collector reads those classes by reflection.
- **`Incubating.Mapping.AttributeMapping`**, the collector's normalize table: `TryGetRename`
  (every renamed key resolved transitively to its final live replacement), `IsVendorPassThrough`
  (every vendor key), `Namespaces` / `NamespaceOf` (the closed value set of
  `qyl.attribute.namespace`), and the four pins as constants. Switch statements over string
  literals, so it stays netstandard2.0-compatible and NativeAOT-clean.
- **Registry policies in Rego**, replacing the Python merge guards: `registry/policies` checks
  the qyl namespace rule and vendor-metadata completeness, `registry/policies-v2` checks the
  namespace enum's coverage of the catalog and the three GenAI invariants
  `emit_analyzer_registry.py` used to raise on.
- **`SemconvDeprecations.g.cs`**, the registry's own deprecations, and
  `DeprecatedSemconvCatalogTests`, which cross-checks the hand-curated
  `OpenTelemetryDeprecatedSemconvCatalog` against it. This replaces
  `verify_deprecated_catalog.py`.

### Changed

- **The provenance header of every generated file** is now one line naming Weaver, the qyl
  schema URL and both upstream pins:
  `// Generated by Weaver 0.26.1 from qyl registry https://qyl.at/schemas/9.0.0 (core v1.44.0, genai fee465db…)`.
  Files whose 8.1.0 header cited the qyl-owned registry (`Elastic`, `Execution`, `Nservicebus`,
  `Quartz`, `Qyl`, the two `MetricAttributes` classes and `QylTelemetryNames`) lose the
  `// Source: SourceGeneration/Resources/qyl-registry.json` line and gain `// Schema:`.
- **The 137 files the 8.1.0 packages shipped regenerate byte-identically** apart from that
  header, with two exceptions, both the dropped `local_attribute_values`:
  `Incubating.Attributes.Messaging.MessagingAttributes` and
  `Incubating.Attributes.Rpc.RpcAttributes`. `SemconvRegistryFacts.g.cs` matches on the same
  terms.
- **The Activity setter extensions ship as `{Root}ActivityExtensions`** under
  `Qyl.Telemetry.SemanticConventions.Activities`, with the same
  `Set{Key}(this Activity, value)` methods and nested `{Key}Values` classes the marker used to
  generate into the consuming assembly. Both packages therefore take
  `System.Diagnostics.DiagnosticSource` on `netstandard2.0`, and `.Incubating` now references
  the stable package for the definition types.
- **The incubating types of the five pre-generated tiers are named `{Root}Incubating…`** —
  `HttpIncubatingActivityExtensions`, `GenAiIncubatingActivityExtensions`,
  `QylIncubatingMetricDefinitions`, `{Root}IncubatingSpanDefinitions`,
  `{Root}IncubatingEventDefinitions`, `{Root}IncubatingEntityDefinitions`, each in the
  `Qyl.Telemetry.SemanticConventions.Incubating.*` namespace it always had, with the nested
  `{Key}Values` classes following their attribute. Together with the disjoint row split this
  is what lets an application `using` a stable and an incubating namespace of the same root
  at once; the earlier shape put an identically named extension method in both and made the
  call ambiguous (CS0121).
- **No generation parameter has a default.** The three schema URLs are read off the
  materialized registry by the template filters; the Weaver version, the core tag, the core
  commit and the genai commit are passed by `scripts/generate.sh` out of `Version.props` and
  `registry/manifest.yaml`. An unset one renders `error: missing param <name>` into the file,
  so a bare `weaver registry generate` cannot produce output that passes for current, and a
  pin cannot be restated in `templates/registry/csharp/weaver.yaml`.
- **The registry pins do not move.** Core stays `v1.44.0` (`e10a930`), genai stays `fee465db`,
  Weaver stays `0.26.1`. No attribute, metric, span, event or entity changes meaning in this
  release; what changes is who generates the code and when.
- **CI** installs Weaver with `open-telemetry/weaver/.github/actions/setup-weaver` and runs
  `scripts/check-generated.sh` before the build. `pin-freshness.yml` and `renovate.json` read
  `registry/manifest.yaml`.

## [8.1.0] - 2026-09-04

### Added

- **Vendor attribute models.** `qyl-registry.json` gains `vendor_models`: one entry per pinned
  third-party library whose native `ActivitySource` qyl subscribes to, declaring the library, the
  exact version qyl pins, the repository and tag the keys were read at, the licence, the source
  names it emits on, and — per attribute — the file and line of the library that sets it. Only keys
  upstream semantic conventions `1.44.0` does not define are declared, and only keys the library
  actually emits. 97 attributes over eleven libraries:

  | Library | Version | ActivitySource(s) | Keys |
  |---|---|---|--:|
  | MassTransit | `8.5.10` | `MassTransit` | 14 |
  | Elastic.Transport | `1.0.0` | `Elastic.Transport` | 7 |
  | Quartz.NET | `4.0.0` | `Quartz` | 13 |
  | NServiceBus | `10.2.9` | `NServiceBus.Core` | 45 |
  | MongoDB.Driver | `3.11.1` | `MongoDB.Driver` | 7 |
  | RabbitMQ.Client | `7.2.2` | `RabbitMQ.Client.Publisher`, `RabbitMQ.Client.Subscriber` | 1 |
  | Npgsql | `10.0.3` | `Npgsql` | 4 |
  | ODP.NET Core | `23.4.0` | `Oracle.ManagedDataAccess.Core` | 6 |
  | MySqlConnector | `2.6.2` | `MySqlConnector` | 0 |
  | MySql.Data | `9.7.0` | `connector-net` | 0 |
  | GraphQL.NET | `8.8.5` | `GraphQL` | 0 |

  The findings, key by key:

  - **MassTransit** `messaging.masstransit.{message_id, correlation_id, request_id, initiator_id,
    source_address, destination_address, input_address, tracking_number, message_types,
    consumer_type, saga_id, begin_state, end_state}` and `peer.address` — declared in
    `DiagnosticHeaders.cs`, set in `LogContextActivityExtensions.cs` (send, receive, consume, saga,
    Courier) and `StateMachineSagaMessageFilter.cs` (the two state tags). `peer.address` is
    MassTransit's own spelling of the consumed message type's diagnostic address and is not an
    upstream key. `messaging.rabbitmq.destination.routing_key` and `messaging.message.body.size`,
    which MassTransit also sets, are already upstream and are not redeclared.
  - **Elastic.Transport** `elastic.transport.{product.name, product.version, version, schema_url,
    attempted_nodes, prepare_request_ms, deserialize_response_ms}` — `DistributedTransport.cs`, the
    two request invokers, and the two response builders. `db.elasticsearch.schema_url` is *not*
    declared: the transport only reads that tag, the client above it writes it.
  - **Quartz.NET** `quartz.{scheduler.name, scheduler.id, fire.instance.id, trigger.group,
    trigger.name, job.type, job.group, job.name, execution.group, jobstore.trigger.count,
    jobstore.batch.size}` on the `Quartz` `ActivitySource` (`QuartzActivitySource.cs`,
    `TracingJobStore.cs`), plus `quartz.{jobstore.operation, cluster.recovered.instance.id}` on the
    same-named `Meter` (`Meters.cs`).
  - **NServiceBus** 32 header-promoted span tags (`ActivityDecorator.cs`'s `HeaderMapping`), five
    written directly (`nservicebus.{native_message_id, handler.handler_type, handler.saga_id,
    event_types, cancelled}`), `nservicebus.outbox.deduplicate-message` (the hyphen is the spelling
    the library emits), and seven tags of the `NServiceBus.Core.Pipeline.Incoming` `Meter`
    (`nservicebus.{discriminator, queue, message_type, message_handler_types, message_handler_type,
    envelope.unwrapper_type}` and `execution.result`).
  - **MongoDB.Driver** `db.mongodb.{lsid, txn_number, server_connection_id, driver_connection_id,
    cursor_id}` plus `db.command.name` and `db.operation.summary`, all in `MongoTelemetry.cs`. The
    last two are `db.*`-shaped but absent from `1.44.0`.
  - **RabbitMQ.Client** `messaging.rabbitmq.delivery_tag` (`RabbitMQActivitySource.cs:213`).
    Upstream spells the same fact `messaging.rabbitmq.message.delivery_tag`; this client does not
    emit that key.
  - **Npgsql** `db.npgsql.{connection_id, data_source, prepared, rows}`
    (`NpgsqlActivitySource.cs`).
  - **ODP.NET Core** `db.odp.{connection.id, roundtrip.count, roundtrip.duration, rows_affected,
    sql_id, user.statement}`. The one model whose finding is a vendor document rather than source at
    a tag — the provider is closed-source — so the note cites Oracle's own attribute table and says
    so.
  - **MySqlConnector**, **MySql.Data** and **GraphQL.NET** declare no attributes: they emit only
    OpenTelemetry keys (pre-stable ones included, which `1.44.0` no longer carries and which
    `qyl.collector.attributes.dropped` now counts). Their models exist for the source name alone.

- **The ActivitySource names of the wave libraries are registry facts.** They land in the merged
  registry as `vendor_scope_names`, ship as `QylTelemetryNames.VendorActivitySources` (stable
  package) — `MassTransit`, `Elastic.Transport`, `Quartz`, `NServiceBus.Core`, `MongoDB.Driver`,
  `RabbitMQ.Client.Publisher`, `RabbitMQ.Client.Subscriber`, `Npgsql`, `MySqlConnector`,
  `connector-net`, `Oracle.ManagedDataAccess.Core`, `GraphQL` — and join QYL0200's known-name
  allowlist, so `AddSource` and a processor's source match carry no literal and the analyzer does
  not report them.

- **`qyl.collector.attributes.dropped`**, a `counter` in `{attribute}`: attributes the collector
  dropped at ingest because their key is not in the pinned registry. Its one required tag is the new
  `qyl.attribute.namespace`, whose value set is **closed** — exactly the attribute namespaces of the
  merged catalog plus `other` (96 members). The collector clamps what it records to that set, so an
  inbound payload cannot fork the series; `merge_registries.py` recomputes the set from the merged
  catalog on every generation and fails, naming the namespace, when the two disagree.

  Both reach the collector as constants, which is what it needs: it may not reference this package
  family and reflects over `*.Attributes.*` types instead. `QylAttributes.AttributeNamespace` and
  the nested `QylAttributes.AttributeNamespaceValues` carry the tag and its closed values, and a new
  emitter projects the qyl-owned instruments as
  `Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl.QylMetricAttributes`
  (`CollectorAttributesDropped`, `CollectorAttributesDroppedUnit`) and
  `…Attributes.Nservicebus.NservicebusMetricAttributes` (`MessagingOperationDuration`,
  `MessagingOperationDurationUnit`). Only rows the registry attributes to qyl are projected this
  way: an upstream instrument is OpenTelemetry's and reaches consumers as a `MetricDefinition` or
  through the embedded registry.

- **`dotnet_wcf` as a local value of `rpc.system.name`**, through the `local_attribute_values`
  mechanism from `7.1.0`: the deprecated `rpc.system` carried it, its `rpc.system.name` replacement
  does not, and qyl's WCF client instrumentation emits it. The `masstransit` and `nservicebus`
  members of `messaging.system` were already declared there and are unchanged.

### Changed

- **The merge rule.** `merge_registries.py` refused every attribute outside `qyl.*`; it now refuses
  every attribute outside `qyl.*` that no `vendor_models` entry declares. `guard_vendor_models`
  fails generation when a model omits its library, version, repository, ref, licence,
  ActivitySources or brief, when an ActivitySource has no finding, when a vendor key is qyl-owned,
  shadows an upstream row, is claimed twice, is not `development`, or carries no citation; and
  `guard_attribute_namespace_enum` holds the closed namespace set to the merged catalog.
  `vendor_catalog_rows` stamps the qyl provenance plus `vendor_library` / `vendor_version` /
  `vendor_ref` on each row, and the merged registry gains `vendor_models` and `vendor_scope_names`
  at its root. `emit_analyzer_registry.py` unions `vendor_scope_names` into the QYL0200 allowlist.
  `emit_typespec_keys.py` is unchanged and still excludes every qyl-sourced row, vendor rows
  included: that projection is the upstream key surface.

- The registry pins do not move. `SemConvSchemaVersion` stays `1.44.0`, `SemConvGenAiRef` stays
  `fee465d`, `WeaverVersion` stays `0.26.1`, so
  [`qyl-references/REFERENCE-STATUS.md`](qyl-references/REFERENCE-STATUS.md) gains no entry: nothing
  upstream changed. Regeneration is idempotent on a second run.

- New generated files, all additive: `ElasticAttributes`, `QuartzAttributes`,
  `NservicebusAttributes` and `ExecutionAttributes` (new roots, incubating tier only — every vendor
  row is `development`), the two `*MetricAttributes` classes, and new members on `DbAttributes`,
  `MessagingAttributes`, `PeerAttributes`, `RpcAttributes` (the `dotnet_wcf` value), `QylAttributes`
  and `QylTelemetryNames`. No member is removed or renamed; the stable tier gains only
  `QylTelemetryNames.VendorActivitySources` and `RpcAttributes`' local value.

## [8.0.1] - 2026-09-04

### Changed

- Every third-party pin moves to its current latest stable, in one wave:

  | Package | Before | After |
  |---|---|---|
  | `ANcpLua.Roslyn.Utilities`, `.Sources`, `.Polyfills`, `.Testing` | `2.2.41` | `2.2.46` |
  | `AwesomeAssertions` | `9.4.0` | `9.6.0` |
  | `xunit.v3.mtp-v2` | `3.2.2` | `4.0.0` |

  `ANcpLua.Roslyn.Utilities.Sources` is a `PrivateAssets="all"` source package, so it compiles
  into the shipped assemblies rather than being declared as a dependency: this is why the pin
  move is a release at all. `Qyl.Telemetry.SemanticConventions.SourceGeneration.dll` and
  `Qyl.Telemetry.SemanticConventions.Analyzers.dll` are rebuilt against `2.2.46`, and neither
  package gains or loses a `<dependency>`. `AwesomeAssertions`, `xunit.v3.mtp-v2` and
  `ANcpLua.Roslyn.Utilities` (the binary) are referenced only by the test projects and reach no
  published package; the xunit major needed no test change.

  Nothing else moves. `SemConvSchemaVersion` stays `1.44.0`, `SemConvGenAiRef` stays `fee465d`
  and `WeaverVersion` stays `0.26.1` — all three are already current upstream, so the registry and
  the generated projection are byte-identical to `8.0.0` and
  [`qyl-references/REFERENCE-STATUS.md`](qyl-references/REFERENCE-STATUS.md) gains no entry.
  `Microsoft.CodeAnalysis` `5.9.0`, `Nuke.Common` `10.1.0`, `NuGet.Packaging` `7.9.0` and
  `System.Security.Cryptography.Xml` `10.0.11` were already latest on nuget.org.

  No public API moves in any of the four packages: no constant, analyzer rule or generator output
  is added, removed or renamed. This is a rebuild, hence a patch.

## [8.0.0] - 2026-09-03

### Added

- `qyl.metricdefinitions.container.incubating.expected.txt`: a second metric-definitions
  byte-identity snapshot, at a root whose metrics carry entity associations. The existing
  `http.server` snapshots pin a root with none, so they cannot see a regression in how
  `entity_associations` is read — the `container` snapshot pins 14 populated `EntityRef`s.

### Changed

- `WeaverVersion` moves from `0.25.1` to `0.26.1`, in the same pin wave. The re-rendered projection
  is identical to the `0.25.1` one except the recorded `weaver_version`, and all 136 files the
  package projections emit are byte-identical, so no member changes and no baseline moves for this
  pin. (`0.26.1` released while this change was in flight; its projection differs from `0.26.0`'s
  only by the recorded `weaver_version`.)

  That holds only because of one fix in the same commit. Weaver 0.26.0 changes
  `entity_associations` from a list of strings to a list of `{"type": ...}` objects.
  `RegistryLoader.ParseStringArray` keeps only string items and drops anything else silently, so
  the new shape parsed as **empty** and every populated `entities:` argument in the generated
  `MetricDefinition`s collapsed to `Array.Empty<EntityRef>()` — 275 metrics across the `container`,
  `k8s.pod`, `process` and `system` roots — with no compile error. `merge_registries.py` now normalises
  `entity_associations` to entity-type strings on groups, metrics and events, keeping the qyl-owned
  projection's documented shape stable across Weaver versions; a shape that is neither string nor
  `{"type": ...}` raises a `MergeError` naming the row instead of being dropped.

- `SemConvGenAiRef` moves from `eaefa14` to `fee465d`, the head of
  open-telemetry/semantic-conventions-genai `main`, absorbing twelve upstream commits. The core
  schema stays `1.44.0` and Weaver is `0.26.1`: the pinned GenAI manifest still declares
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

  Both pins' deltas are recorded in
  [`qyl-references/REFERENCE-STATUS.md`](qyl-references/REFERENCE-STATUS.md),
  which this change also introduces — the path
  [`check_pin_freshness.py`](src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/check_pin_freshness.py)
  has always named in its stale-pin report, but which no commit had created.

### Removed

**Breaking (`8.0.0`).** The qyl-owned observer vocabulary. Its only producer, the qyl Codex
observer, is gone; nothing emits or reads these names any more, so they are deleted outright
rather than deprecated. Removed in one wave with qyl `3.0.0`, `qyl.mcp` `4.0.0` and
`qyl-api-schema` `9.0.0`.

- Nine `qyl.agent.diagnostic.*` attributes drop out of
  [`Resources/qyl-registry.json`](src/Qyl.Telemetry.SemanticConventions.SourceGeneration/Resources/qyl-registry.json)
  and therefore out of the merged projection, the QYL0200/QYL0201 allowlists and the shipped
  constants: `qyl.agent.diagnostic.extension.id`, `qyl.agent.diagnostic.snapshot.id`,
  `qyl.agent.diagnostic.format.version`, `qyl.agent.diagnostic.probe.id`,
  `qyl.agent.diagnostic.phase` (with its four enum members), `qyl.agent.diagnostic.outcome`
  (with its four enum members), `qyl.agent.diagnostic.variable.count`,
  `qyl.agent.diagnostic.check.count` and `qyl.agent.diagnostic.check.failed_count`.

- Five `qyl.workflow.*` correlation attributes go with them: `qyl.workflow.run.id`,
  `qyl.workflow.event.id`, `qyl.workflow.attempt.id`, `qyl.workflow.agent.id` and
  `qyl.workflow.tool_call.id`.

- The event name `qyl.agent.diagnostic.snapshot`. The two surviving qyl-owned event names,
  `qyl.http.client` and `qyl.rpc.grpc`, are untouched, as are all six `scope_names` and the one
  qyl-owned metric — that metric references only `messaging.*` attributes, so nothing it needs
  was deleted.

  The projected public surface loses exactly sixteen members from
  `Qyl.Telemetry.SemanticConventions.Incubating.Attributes.Qyl.QylAttributes` —
  `AgentDiagnosticExtensionId`, `AgentDiagnosticSnapshotId`, `AgentDiagnosticFormatVersion`,
  `AgentDiagnosticProbeId`, `AgentDiagnosticPhase`, `AgentDiagnosticPhaseValues`,
  `AgentDiagnosticOutcome`, `AgentDiagnosticOutcomeValues`, `AgentDiagnosticVariableCount`,
  `AgentDiagnosticCheckCount`, `AgentDiagnosticCheckFailedCount`, `WorkflowRunId`,
  `WorkflowEventId`, `WorkflowAttemptId`, `WorkflowAgentId` and `WorkflowToolCallId` — plus
  `QylTelemetryNames.Events.QylAgentDiagnosticSnapshot` from the stable package. The two
  `…Values` classes take their four constants each with them. Nothing is renamed and no
  `[Obsolete]` shim is left behind: of the 131 files pinned by
  `qyl.package.manifest.sha256`, only `QylAttributes.g.cs` and `QylTelemetryNames.g.cs` change.

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
- `UPSTREAM-dotnet_wcf.md`: a draft issue for
  open-telemetry/semantic-conventions asking where `dotnet_wcf` belongs now that `rpc.system` is
  renamed to `rpc.system.name`, which declares no WCF member. Not filed, withdrawn on 2026-09-04: upstream PR 3176 left WCF out on purpose pending WCF conventions; `dotnet_wcf` is unchanged
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
