# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line and must equal the registry
`schema_url` version, which qyl.at serves before a release. A release is packed and pushed from the developer's machine
(the three NuGet packages and the TypeScript npm package), then tagged `v<version>`;
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the published packages against nuget.org.

## [10.0.0]

The agent-first API of the qyl API DSL ADR (2026-10-14): a breaking release.

- Registry policy (`registry/qyl/policy.yaml`, handed to the templates as `-D policy=<json>`): `qyl.persisted: false` on the 42 keys the collector's `deniedExactKeys` listed (all but `gen_ai.agent.name`, which is now persisted), the denied-key tokens, the token-exempt keys, `qyl.phase: end` on the attributes known only when an operation ends, and the `qyl.span_name` chains and starters keyed by span refinement id.
- New `RegistryPolicy` table in the stable package (`NotPersistedKeys`, `NotPersistedPrefixes`, `DeniedKeyTokens`, `TokenExemptKeys`, `VendorKeys`, `IsPersisted`, `ContainsDeniedToken`); the qyl collector's catalog reads it instead of its own deny lists.
- Span definitions are keyed by refinement id: the gen_ai skill spans (`gen_ai.execute_tool.load_skill.internal`, `.read_skill_resource.internal`, `.command.internal`), `db.{mariadb,mysql,oracledb,postgresql,sql_server}.client`, the `messaging.<system>.*`, `dynamodb.*` and `aws.s3.client` rows are generated; the GenAI provider refinements stay out. `SpanDefinition` gains `Type`, `IsSpecialization` and `SpanName` (the name chain); its constructor changes.
- `AttributeRef` gains `Condition`, `Phase` (new `AttributePhase`) and `IsPersisted`; its constructor changes.
- Span starters (namespace `Qyl`): `Spans.DbClient` in the stable package and `IncubatingSpans.MessagingProcessConsumer`, `GenAiInvokeAgent`, `GenAiExecuteTool` and the skill specializations `GenAiExecuteToolLoadSkill`, `GenAiExecuteToolReadSkillResource`, `GenAiExecuteToolCommand` in Incubating. Required attributes are positional, conditionally required ones nullable without a default (omitting one is CS7036), recommended ones default to `null`; OptIn, end-phase and group-fixed attributes take no parameter. Each starter stamps its `qyl.instrumentation.domain`, takes its kind from the group, its name from the `qyl.span_name` chain, puts every supplied value in the creation tags, and returns `null` when nothing listens; the consumer starter takes the producer's `ActivityContext`.
- `Spans.Source`: the public `Qyl.Telemetry.Application` ActivitySource, version `SchemaUrl.QylVersion`, schema URL `SchemaUrl.Qyl`. `Spans.Work(name)` starts an internal span stamped domain `app`. `Spans.SetAppTag(key, value)` (string, long, double, bool) writes `qyl.app.<key>`.
- `Spans.RecordFailure(exception)` and `RecordFailure(ErrorType, description)`: status Error with a description bounded to `Spans.MaxStatusDescriptionLength` (256), `error.type`, and for an exception the exception event.
- `Qyl.SpanNames` (stable package): one method per `qyl.span_name` chain, for db, http, messaging, rpc, gen_ai and mcp spans, so interceptors and starters name spans the same way (`SpanNames.HttpServer("_OTHER", "/orders")` is `HTTP /orders`).
- Value sets are open structs: every enum attribute gets a `readonly struct` in the Activities namespace (`DbSystemName`, `ErrorType`, `HttpRequestMethod`, ...) with one static member per registry value, `Custom(...)`, `Value`, `Key` and value equality, and no conversion from `string`. The typed setters of enum attributes take the struct (`SetDbSystemName(DbSystemName.Postgresql)`). The 91 nested `*Values` classes in `Generated/Activities` are deleted; the `const string` classes under `Generated/Attributes` stay.
- No `Activity` setter for a resource-only attribute (referenced by entities and by no span, event or metric: `service.name`, `deployment.environment.name`, `host.*`, `k8s.*`, `telemetry.sdk.*`, ...); the `TelemetryActivityExtensions` and eight incubating extension classes disappear with them. No generic `SetQylApp` either.
- `scripts/generate.sh` clears both Generated trees before Weaver writes them, so an output the registry stops producing leaves no stale file.
- Fix: a `{recommended: <condition>}` requirement level maps to `Recommended`, not `ConditionallyRequired` (for example `network.peer.address` on `db.client`).
- Registry: the `qyl.app` template attribute (business keys, `qyl.app.<key>`), the `qyl.instrumentation.domain` values `app`, `messaging.process`, `gen_ai.invoke_agent` and `gen_ai.execute_tool`, and the scope `Qyl.Telemetry.Application` (`QylTelemetryNames.Scopes.QylTelemetryApplication`).
- `SchemaUrl.Qyl` and `SchemaUrl.QylVersion`: the qyl registry's schema URL, `https://qyl.at/schemas/10.0.0`.
- Give every string attribute of the qyl and vendor registries examples taken from the code that writes it, and declare the manifest's `file_format`; Weaver no longer reports diagnostics for this registry.
- Build: the pipeline tests run under Microsoft code coverage, and `./build.sh Coverage` (in `mise run check-full`) gates line and branch coverage of this repository's sources through DotCov.Fallout 4.1.0.

## [9.9.2] - 2026-10-14

- Move ANcpLua.Roslyn.Utilities from 2.2.47 to 2.2.48: the analyzers embed `ANcpLua.Roslyn.Utilities.Sources` 2.2.48.
- Replace the copied analyzer test harness with `ANcpLua.Roslyn.Utilities.Testing` 2.2.48 (`Compile`, `DictionaryAnalyzerConfigOptionsProvider`, `SourceFixture`, `RepositoryRoot`); the tests and their assertions are unchanged.
- No public API, analyzer rule or generated vocabulary change.

## [9.9.1] - 2026-10-12

- Run analyzer documentation checks from the completed generator binary; remove nested `dotnet run` builds.
- Define `qyl.gen_ai.tool.call.arguments_hash` once in the shared registry for producer-supplied, opaque argument fingerprints.
- Generate matching C# and TypeScript constants; no raw argument capture or instrumentation behavior changes.

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).
