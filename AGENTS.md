# Qyl.OpenTelemetry.SemanticConventions engineering contract

This is the repository's only editable agent/contributor instruction file.
`CLAUDE.md` is a symlink to it. Keep package guidance in the root or packaged
README, released history in releases, and generated analyzer documentation under
`docs/`. Do not add mission prompts, progress diaries, or a second rules file.

## Purpose and compatibility

This repository turns pinned OpenTelemetry semantic-convention registries into
stable/incubating .NET constants, Roslyn source-generation APIs, analyzer rules, and
the TypeSpec key projection consumed by `qyl-api-schema`.

The packages are public. Published NuGet artifacts are immutable. Breaking
surface cleanup uses a new major version and migrates known consumers; do not add
shims without a proven external requirement.

## One owner for every generated surface

- `Version.props` owns the core semantic-convention registry and Weaver pins.
- `src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/generate.sh`
  owns the Weaver-resolved registry embedded by the packages.
- `emit_attributes.py` owns the committed stable and incubating constant trees.
- `emit_typespec_keys.py` owns
  `qyl-api-schema/generated/otel-keys.gen.tsp`; regenerate that sibling projection
  from the same resolved registry after a pin change.
- `DocsGenerator` owns the analyzer index, migration catalog, SARIF/editorconfig
  artifacts, and per-rule HelpLink pages.
- `AnalyzerReleases.Shipped.md` and `AnalyzerReleases.Unshipped.md` are maintained
  analyzer inputs, not generated prose.

Never patch generated output. Change the registry input, generator, analyzer, or
documentation generator, regenerate, and commit owner plus outputs together.

Stable constants contain stable and deprecated upstream entries. Incubating constants
contain the unstable surface and may break between minor releases. Development GenAI
conventions remain incubating until their upstream stability changes.

## Contract boundaries

This repository owns telemetry vocabulary, not Qyl product API models and not OTLP
wire messages. The TypeSpec key projection supplies names to `qyl-api-schema`; the
schema repository owns every Qyl client-visible request, response, stream event, and
error. Do not move AutoInstrumentation's internal capability catalog into the public
product-contract repository.

A source-generation or analyzer API needs an executable consumer and tests for the
complete contract. Marker-only declarations, unexecuted sample code, or a mock that
only reproduces the expected string are not acceptance evidence.

The analyzer project is preview-only. Qyl does not consume it, and only seven of its 48
rules have executable behavior tests. Keep it out of stable packs. An explicit preview
pack requires `PackPreviewAnalyzers=true` and a prerelease package version. Stable
promotion requires Qyl consumption and executable coverage for every rule.

## Build and regeneration

Build all projects with warnings treated as errors:

```bash
dotnet build Qyl.OpenTelemetry.SemanticConventions.slnx -c Release
```

Run both Microsoft Testing Platform executables:

```bash
dotnet run --project tests/Qyl.OpenTelemetry.SemanticConventions.Pipeline.Tests -c Release
dotnet run --project tests/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests -c Release
```

Important generation gates:

```bash
./build.sh VerifyAttributesHash
./build.sh CheckDocs
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/verify_deprecated_catalog.py
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/emit_analyzer_registry.py --check
python3 src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/emit_registry_resources.py --check
```

After an intentional attribute regeneration, reseed the manifest with
`./build.sh SeedAttributesHash`. Regenerate analyzer documentation with
`./build.sh GenerateDocs`; never edit generated rule pages directly.

## Publishing

Publication is GitHub Actions OIDC trusted publishing. Never add a long-lived NuGet
API key or publish locally. The supported release set is the stable constants,
incubating constants, and source generator. Release work builds, tests, packs,
inspects, waits for registry indexing, restores into a clean consumer, executes it,
and only then tags and announces the release. The committed local version is a
development fallback; the release workflow owns the published version.
