# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line and must equal the registry
`schema_url` version, which qyl.at serves before a release. A release is packed and pushed from the developer's machine
(the three NuGet packages and the TypeScript npm package), then tagged `v<version>`;
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the published packages against nuget.org.

## [9.9.3]

- Give every string attribute of the qyl and vendor registries examples taken from the code that writes it, and declare the manifest's `file_format`; Weaver no longer reports diagnostics for this registry.
- `RegistrySnapshot.Sha256` and `REGISTRY_SNAPSHOT_SHA256` change with the examples; no constant name, value set, doc comment or analyzer rule changes.
- Build: the pipeline tests run under Microsoft code coverage, and `./build.sh Coverage` (in `mise run check-full`) gates line and branch coverage through DotCov.Fallout 4.1.0.

## [9.9.2] - 2026-10-14

- Move ANcpLua.Roslyn.Utilities from 2.2.47 to 2.2.48: the analyzers embed `ANcpLua.Roslyn.Utilities.Sources` 2.2.48.
- Replace the copied analyzer test harness with `ANcpLua.Roslyn.Utilities.Testing` 2.2.48 (`Compile`, `DictionaryAnalyzerConfigOptionsProvider`, `SourceFixture`, `RepositoryRoot`); the tests and their assertions are unchanged.
- No public API, analyzer rule or generated vocabulary change.

## [9.9.1] - 2026-10-12

- Run analyzer documentation checks from the completed generator binary; remove nested `dotnet run` builds.
- Define `qyl.gen_ai.tool.call.arguments_hash` once in the shared registry for producer-supplied, opaque argument fingerprints.
- Generate matching C# and TypeScript constants; no raw argument capture or instrumentation behavior changes.

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).
