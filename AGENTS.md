# AGENTS.md

Agent/contributor guide for **`Qyl.OpenTelemetry.SemanticConventions`**.
`CLAUDE.md` is a symlink to this file — edit here once, every tool (Claude Code,
Copilot, Gemini) sees it.

## Shipped packages (5, all `Qyl.OpenTelemetry.SemanticConventions*`)

`.` (main) · `.Incubating` · `.SourceGeneration` · `.Analyzers` · `.Nuke`

Published to nuget.org via **trusted publishing** on `v*` tag push (or
`workflow_dispatch` with a `version` input): the workflow packs the whole
solution at the tag version and pushes every `.nupkg` with `--skip-duplicate`.

**Current version state — the next `v3.0.1` tag is safe (no action needed).**
`Qyl.OpenTelemetry.SemanticConventions.Nuke` is already on nuget at **3.0.1**
(published out-of-band when the component was re-vendored), while the other four
packages are still at **3.0.0**. Tagging `v3.0.1` will pack all five at 3.0.1 and,
thanks to `--skip-duplicate`, skip the already-published Nuke 3.0.1 while
publishing the other four — so the family converges to 3.0.1 with no conflict and
no special handling.

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
- That package's repo (`O-ANcppLua/ANcpLua.OpenTelemetry.Conventions.Nuke`) is
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
  `ANcpLua.Roslyn.Utilities.Polyfills` source package, not the BCL.
  (Rider may falsely flag `System.Range` "not resolved" on the **net10**
  DocsGenerator — that's a ReSharper cross-project quirk; `dotnet build` is truth.)
