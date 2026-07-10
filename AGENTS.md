# AGENTS.md

Agent/contributor guide for **`Qyl.OpenTelemetry.SemanticConventions`**.
`CLAUDE.md` is a symlink to this file — edit here once, every tool (Claude Code,
Copilot, Gemini) sees it.

## Shipped packages (5, all `Qyl.OpenTelemetry.SemanticConventions*`)

`.` (main) · `.Incubating` · `.SourceGeneration` · `.Analyzers` · `.Nuke`

Published to nuget.org via **trusted publishing** on `v*` tag push (or
`workflow_dispatch` with a `version` input): the workflow packs the whole
solution at the tag version and pushes every `.nupkg` with `--skip-duplicate`.

**Current version state — the family is converged at `3.3.0`.** `v3.3.0`
(tagged on `main`) ships the **full 1.43.0 attribute surface**: the `3.2.0`
upgrade regenerated only `GenAiAttributes.g.cs`, leaving every other
`Attributes/*.g.cs` a stale ~1.41-era fossil while `SchemaUrl.g.cs` already
claimed 1.43.0. `3.3.0` regenerates the whole surface — the stable package gains
the 1.43 graduations (K8s, Container, Net, System, Vcs, Messaging, Az, …) and
incubating picks up all 1.42/1.43 additions. `3.2.0` and earlier remain the
prior published line. The next release bumps `VersionPrefix` in
`Directory.Build.props` (the local-restore fallback) and tags — the workflow
packs all five at the tag version and `--skip-duplicate` makes re-runs
idempotent. (Note: the `v3.0.2` tag points at an orphaned commit not on `main`;
3.0.2 published fine and the tag stays as the historical release marker.)

## Regenerating the shipped attribute surface (`emit_attributes.py`)

The shipped constant packages (`.` + `.Incubating`) use a **compact** emitter
distinct from the `.SourceGeneration` Roslyn generator (`AttributesEmitter`,
contrib-shape `Attribute*` members). That compact emitter was ad-hoc and
uncommitted until `3.3.0`; it now lives at
`src/…SourceGeneration/scripts/emit_attributes.py`. It reads the embedded
`Resources/resolved-registry.json` and regenerates BOTH `Attributes/` trees:
`--stdout {root} {stable|incubating}` prints one file; `--write` rewrites both
trees. Stable tier = `stable`/`deprecated` rows only (mirrors
`StabilityFiltering.IsIncludedOrDeprecated`); incubating = all. It resolves
`.`↔`_` deprecated-alias PascalCase collisions (keep canonical, drop the
deprecated twin), escapes `<>`/`&` in doc comments (valid XML — malformed XML
makes the compiler drop the whole doc comment), and self-checks doc-XML
well-formedness. **After any registry bump, run `./…/emit_attributes.py --write`
then `./build.sh SeedAttributesHash` — regenerate the whole surface, not one
file** (that omission is exactly what left `3.2.0` stale).

## GenAI registry: separate upstream repo, development-only, Incubating-only

The GenAI conventions moved to their own upstream repo,
**`open-telemetry/semantic-conventions-genai`** (cloned at
`~/RiderProjects/qyl-references/semantic-conventions-genai`; see the
workspace router `../CLAUDE.md`).
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
