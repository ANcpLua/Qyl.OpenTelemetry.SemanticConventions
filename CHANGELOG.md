# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line; the publish
workflow stamps the published version from the `v*` tag that triggers it. That tag runs the release
gate: CI packs the solution, publishes through NuGet trusted publishing, and
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the indexed packages in a
clean `net10.0` consumer.

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
