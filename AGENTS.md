# AGENTS.md

Agent/contributor guide for **`Qyl.OpenTelemetry.SemanticConventions`**.
`CLAUDE.md` is a symlink to this file — edit here once, every tool (Claude Code,
Copilot, Gemini) sees it.

## Shipped packages (5, all `Qyl.OpenTelemetry.SemanticConventions*`)

`.` (main) · `.Incubating` · `.SourceGeneration` · `.Analyzers` · `.Nuke`

Published to nuget.org via **trusted publishing** on `v*` tag push (or
`workflow_dispatch` with a `version` input): the workflow packs the whole
solution at the tag version and pushes every `.nupkg` with `--skip-duplicate`.

**Current version state — the family is converged at `3.2.0`.** `v3.2.0` is
tagged on `main` (the OpenTelemetry semconv **1.43.0** + development GenAI
registry upgrade) and all five packages publish on nuget.org at **3.2.0**
(`.`, `.Incubating`, `.SourceGeneration`, `.Analyzers`, `.Nuke`); `3.1.0` and
earlier remain the prior published line. The next release is `v3.2.1`/`v3.3.0`:
bump `VersionPrefix` in `Directory.Build.props` (the local-restore fallback) to
match, then tag — the workflow packs all five at the tag version and
`--skip-duplicate` makes re-runs idempotent. (Note: the `v3.0.2` tag points at
an orphaned commit not on `main`; 3.0.2 published fine and the tag stays as the
historical release marker.)

## GenAI registry: separate upstream repo, development-only, Incubating-only

The GenAI conventions moved to their own upstream repo,
**`open-telemetry/semantic-conventions-genai`** (cloned at
`../semantic-conventions-genai`; see the workspace router `../CLAUDE.md`).
Facts that matter here:

- **Everything in that registry is `stability: development`** (registry-level
  `stability: development` in `model/manifest.yaml`; zero stable entries).
  Accordingly `gen_ai.*` keys generate into **`.Incubating` only** — if a
  `gen_ai.*` constant ever appears in the stable package, the
  `StabilityFilter.StableOnly` pipeline is broken, not the registry.
- Its manifest declares a **dev schema family**
  (`https://opentelemetry.io/schemas/gen-ai-dev/1.42.0-dev`) while the README
  still says "Schema URL: TODO", and it builds against a **filtered core
  registry pinned at 1.41.0** (gen-ai/mcp/openai dirs stripped upstream-side to
  avoid duplicate group ids). This repo pins core semconv **1.43.0** — the skew
  is expected and fine while GenAI is development.
- **When the genai repo publishes a proper (non `-dev`) schema URL, that is a
  future `Version.props` decision** for this repo: whether to pin the GenAI
  registry version separately from `SemConvSchemaVersion` instead of treating
  it as "the development GenAI registry" folded into the family version.

## The Nuke build component lives in THIS repo — do not re-externalize it

`src/Qyl.OpenTelemetry.SemanticConventions.Nuke/` is the build component
(`IUpstreamConventions`, `IDomainConventionsApi`, `LockstepPolicy`, `Helpers`,
`ParameterDefaults`). `eng/build/_build.csproj` and the Pipeline.Tests consume it
via `ProjectReference`, and the build host **dogfoods** it
(`./build.sh VerifyAttributesHash` parses a `{semconv}-{n}` version through
`LockstepPolicy`).

History worth knowing before you "tidy" this:
- `4f7434e` (2026-05-26) swapped the local project out for the external
  **`ANcpLua.OpenTelemetry.Conventions.Nuke`** 0.1.0 package.
- That package's repo (`ANcpLua/ANcpLua.OpenTelemetry.Conventions.Nuke`) is
  **archived** (frozen at 0.1.0), so on 2026-05-29 it was **re-vendored back**
  (`21db20f`). The two are the same component; only the namespace differed
  (`Qyl.OpenTelemetry.SemanticConventions.Nuke` vs `ANcpLua.OpenTelemetry.Conventions.Nuke`).

**Do NOT** re-swap to `ANcpLua.OpenTelemetry.Conventions.Nuke` — it's archived.
Edit `LockstepPolicy` and friends here; this project ships as the `.Nuke` package.
(`Qyl…Nuke` 3.0.1 was published from this restored source; the orphaned 3.0.0 is deprecated.)

## Build / verify gotchas

- `dotnet build Qyl.OpenTelemetry.SemanticConventions.slnx` must be **0/0**
  (`TreatWarningsAsErrors` is on).
- **Tests are Microsoft.Testing.Platform (exe-style).** On the .NET 10 SDK,
  `dotnet test` errors with a VSTest message — run them as the MTP executable:
  `dotnet run --project tests/<proj>.csproj -c Debug`.
- `./build.sh CheckDocs` fails if `docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md`
  drifts from the analyzer assembly (regenerate with `./build.sh GenerateDocs`;
  the generator is `tools/…DocsGenerator`, never hand-edit the markdown).
- The `.Analyzers` and `.SourceGeneration` projects target **netstandard2.0**;
  `System.Index`/`System.Range`/`IsExternalInit` come from the
  `ANcpLua.Roslyn.Utilities.Polyfills` source package, not the BCL. Those polyfills
  compile as `internal` types into the analyzer assembly, so any project that gets
  `InternalsVisibleTo` from `.Analyzers` **and** targets a modern TFM must reference
  it with `Aliases="analyzers"` (see the DocsGenerator's `ProjectReference`) —
  otherwise the friend assembly's `System.Range` collides with the BCL's and the
  IDE can't resolve the type.
