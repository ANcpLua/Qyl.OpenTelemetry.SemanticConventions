# Qyl.Telemetry.SemanticConventions: release, pins and agent hand-offs

Who reads this: the one agent working on this repository's `main`. Agent, user and owner are
the same word. There is one agent per repository, six in all (qyl, qyl.mcp, qyl-api-schema,
Qyl.Telemetry.AutoInstrumentation, Qyl.Telemetry.SemanticConventions, qyl.at), each on its own
`main`. Commit and push to `main` directly: no pull requests, no CI, no branch rules. The gate
is `mise run check`; the registry is regenerated with `scripts/generate.sh`, which needs the
Weaver version pinned in `Version.props`.

## This repository

- Publishes exactly four artifacts at one version: three NuGet packages,
  `Qyl.Telemetry.SemanticConventions`, `.Analyzers` and `.Incubating`, and the npm package
  `@ancplua/qyl-semantic-conventions` from `typescript/`. No runtime dependencies or Zod
  belong in the TypeScript package. Both languages are generated from the resolved registry.
- Version sources that must agree on `main`: `Directory.Build.props` `VersionPrefix`,
  `registry/manifest.yaml` `schema_url` (`https://qyl.at/schemas/<x.y.z>`) and a `## [x.y.z]`
  entry in `CHANGELOG.md`; run `scripts/generate.sh` after the bump and commit its output. The
  CHANGELOG entry becomes the GitHub release notes.
- Precondition that belongs to another agent: qyl.at must serve
  `https://qyl.at/schemas/<x.y.z>` before the push. Send qyl.at's agent the schema request
  first (section "Notify", message type SCHEMA), wait until the URL returns 200, then release.
- Release command: `./release.sh <x.y.z> --dry-run`, then `./release.sh <x.y.z>` (restore,
  build and pack the solution at that version, the exact three-NuGet plus one-npm artifact guard, publish,
  wait for all four registry indexes, isolated NuGet and npm consumer verification, tag and GitHub release).
- Keys: npm uses `~/.config/vitals/npm-qyl-manual-publish-token`; NuGet uses the `vitals keys` entry "qyl-dev manual publish and release (NuGet)", file
  `~/.config/vitals/nuget-qyl-manual-publish-key`. The script reads it; never print, paste,
  commit or log a key. If the entry is missing or its note's expiry has passed, stop and tell
  the owner. A rejected key or npm OTP request also stops publication. Never print key values.
- Upstream pins: the OpenTelemetry registry inputs in `registry/manifest.yaml` and
  `scripts/fetch-core.sh`; no qyl-dev package.
- Consumers to notify after all four packages are indexed: Qyl.Telemetry.AutoInstrumentation
  (`Directory.Packages.props`, `SEMCONV_REF`), qyl (`Version.props`
  `QylSemanticConventionsVersion`), qyl.mcp (`server/package.json` and `bun.lock`) and qyl.at (`src/data/site.ts`).

## Before any work: the stale-version guard

1. `git pull --ff-only origin main`.
2. `mise install`, then `dotnet restore`; `bun install --frozen-lockfile` inside `typescript/`
   when working there.
3. Check the last entry of
   `curl -fsSL https://api.nuget.org/v3-flatcontainer/qyl.telemetry.semanticconventions/index.json`;
   `VersionPrefix` on `main` is either that version or the next one being prepared. Never reuse
   a published version.

## Releasing

1. Land the bump commit on `main`: `VersionPrefix`, `schema_url`, CHANGELOG entry, regenerated
   output.
2. Ask qyl.at for the schema (SCHEMA message) and wait for
   `curl -fsS https://qyl.at/schemas/<x.y.z>` to return 200.
3. `./release.sh <x.y.z> --dry-run`; fix whatever it refuses.
4. `./release.sh <x.y.z>`.
5. Watch indexing: the script runs `eng/release/verify-packages.sh` against nuget.org and polls
   the index. Keep polling every 30 s until all three NuGet ids and the npm package list `<x.y.z>`. Until then, do
   nothing else about this release.
6. The moment all four are indexed: stop watching and notify the consumers (section
   "Notify").
7. Record the published version in
   `/Users/alexandernachtmann/Downloads/qyl-codex-claude/qyl-handoff-for-codex.md` and rewrite
   `/Users/alexandernachtmann/Downloads/qyl-codex-claude/qyl-published-versions.yaml` (the
   registries are the truth; this file is the snapshot every agent reads first).

## Notify

One message per consumer repo, to its running agent through the session's agent-messaging
tool when there is one (Claude Code: `ListAgents`, then `SendMessage`). If the consumer has
no running agent, append the same text to the handoff file above under "Pending re-pins";
the owner relays it. Two message types:

```
SCHEMA Qyl.Telemetry.SemanticConventions <version> needs https://qyl.at/schemas/<version>
repo: qyl.at
add: public/schemas/<version> (schema_url https://qyl.at/schemas/<version>), then ./release.sh
```

```
RELEASE Qyl.Telemetry.SemanticConventions (3 NuGet packages + 1 npm package) <version> indexed <UTC time>
repo: <Qyl.Telemetry.AutoInstrumentation | qyl | qyl.mcp | qyl.at>
re-pin: <Directory.Packages.props + SEMCONV_REF | Version.props QylSemanticConventionsVersion | server/package.json + bun.lock | src/data/site.ts>
then: regenerate the lockfile, run the gate, commit, push main
```

## When a release lands and you are the consumer

This repo consumes no qyl-dev package. When upstream OpenTelemetry conventions move, update
`registry/manifest.yaml`, run `scripts/fetch-core.sh` and `scripts/generate.sh`, run
`mise run check`, commit, push `main`.

## Order rules that never change

- qyl.at serves the schema before package publication.
- This repo and AutoInstrumentation both feed qyl: qyl re-pins both in one commit once both
  are indexed, so tell qyl's agent whether AutoInstrumentation moves in the same wave.
- qyl.at lists a version only after the index confirms it.
