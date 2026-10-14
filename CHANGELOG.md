# Changelog

Notable changes to the `Qyl.Telemetry.SemanticConventions` package family. `VersionPrefix` in
[`Directory.Build.props`](Directory.Build.props) names the current release line and must equal the registry
`schema_url` version, which qyl.at serves before a release. A release is packed and pushed from the developer's machine
(the three NuGet packages and the TypeScript npm package), then tagged `v<version>`;
[`eng/release/verify-packages.sh`](eng/release/verify-packages.sh) proves the published packages against nuget.org.

## [9.9.2] - 2026-10-14

- Move ANcpLua.Roslyn.Utilities from 2.2.47 to 2.2.48: the analyzers embed `ANcpLua.Roslyn.Utilities.Sources` 2.2.48.
- Replace the copied analyzer test harness with `ANcpLua.Roslyn.Utilities.Testing` 2.2.48 (`Compile`, `DictionaryAnalyzerConfigOptionsProvider`, `SourceFixture`, `RepositoryRoot`); the tests and their assertions are unchanged.
- No public API, analyzer rule or generated vocabulary change.

## [9.9.1] - 2026-10-12

- Run analyzer documentation checks from the completed generator binary; remove nested `dotnet run` builds.
- Define `qyl.gen_ai.tool.call.arguments_hash` once in the shared registry for producer-supplied, opaque argument fingerprints.
- Generate matching C# and TypeScript constants; no raw argument capture or instrumentation behavior changes.

## [9.9.0] - 2026-10-12

- Generate stable and incubating TypeScript bindings from the same resolved registry as C#.
- Add the dependency-free `@ancplua/qyl-semantic-conventions` package and packed-consumer checks.
- Publish all four artifacts at one version, require their exact names, and verify installed public consumers.
- Make the shared registry fingerprint independent of local source paths and prove full packed TypeScript/C# vocabulary parity.
- Correct C# integer enum constants, value sets, Activity setters and analyzer type facts to preserve registry numeric values, matching TypeScript.
- Declare the existing `qyl.mcp.operation` event in the shared registry.

Older entries: [CHANGELOG.archive.md](CHANGELOG.archive.md).
