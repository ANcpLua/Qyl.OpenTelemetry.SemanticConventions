# Qyl.Telemetry.SemanticConventions: release and pins

One agent per qyl-dev repo (qyl, qyl.mcp, qyl-api-schema, Qyl.Telemetry.AutoInstrumentation, Qyl.Telemetry.SemanticConventions, qyl.at), each on its own `main`; agent, user and owner are one word. No PR and no publishing workflow; CI only reports: releases run on this machine with the keys in Vitals (`vitals keys`).

## This repository

- Publishes four artifacts at one version: NuGet `Qyl.Telemetry.SemanticConventions`, `.Analyzers` and `.Incubating`, and npm `@ancplua/qyl-semantic-conventions` from `typescript/` (no runtime dependencies, no Zod).
- Version sources that agree on `main`: `Directory.Build.props` `VersionPrefix`, `registry/manifest.yaml` `schema_url` (`https://qyl.at/schemas/<x.y.z>`), `typescript/package.json` `version`, and a `## [x.y.z]` entry on top of `CHANGELOG.md` (the GitHub release notes). Run `scripts/generate.sh` after the bump and commit its output.
- qyl.at serves `https://qyl.at/schemas/<x.y.z>` before the push; `release.sh` creates and deploys it through the qyl.at clone (`../qyl.at` or `QYL_AT_DIR`) when missing.
- Keys: npm `~/.config/vitals/npm-qyl-manual-publish-token`; NuGet Vitals entry "qyl-dev manual publish and release (NuGet)", `~/.config/vitals/nuget-qyl-manual-publish-key`; read by the script. Never print, paste, commit or log a key. Entry missing, note expired, a rejected key or an npm one-time-password prompt: stop and tell the owner.
- Upstream pins: the OpenTelemetry registry inputs in `registry/manifest.yaml` and `scripts/fetch-core.sh`. When they move: `scripts/fetch-core.sh`, `scripts/generate.sh`, `mise run check`, commit, push `main`.

## Stale-version guard, before any work

1. `git pull --ff-only origin main`, `mise install`, `dotnet restore`; `bun install --frozen-lockfile` in `typescript/` when working there.
2. `VersionPrefix` is the last entry of `curl -fsSL https://api.nuget.org/v3-flatcontainer/qyl.telemetry.semanticconventions/index.json` or the next one; never reuse a published version.

## Release

1. Land the bump commit (version sources, regenerated output) on `main`.
2. `./release.sh <x.y.z>` (`--dry-run` first only when a step is new or unsure; the script refuses before publishing when anything is off). It returns before indexing, once NuGet and npm accepted the push and the tag and GitHub release exist; a detached job runs `./build.sh VerifyRelease --published-version <x.y.z>` (index wait for all four, release smoke against the registries) and then the snapshot refresh (`published-versions.yaml` in qyl.at), logging to `artifacts/release-<x.y.z>-verify.log`. Do not run the snapshot refresh by hand.
3. `./release.sh --wait <x.y.z>` blocks until that log ends in VERIFIED or FAILED; nothing else about this release until then. Exit 3 = not indexed in time, `./release.sh --verify <x.y.z>` checks again; any other FAILED after publication is a patch release.
4. VERIFIED: notify the consumers.

Stopped after NuGet accepted the packages: keep the four artifacts and `release-<version>.json`, renew credentials, wait until all three NuGet ids are indexed, then `--resume` as the script header shows, dry run first. `--browser-auth` only after the owner explicitly offers to complete browser authentication.

## Notify

| Consumer | Re-pins |
|---|---|
| Qyl.Telemetry.AutoInstrumentation | `Directory.Packages.props` (single source; the registry tag derives from it) |
| qyl | `Version.props` `QylSemanticConventionsVersion` |
| qyl.mcp | `server/package.json`, `workbench/package.json`, `bun.lock` |
| qyl.at | `src/data/site.ts` |

One message per consumer through the session's agent-messaging tool (Claude Code: `ListAgents`, then `SendMessage`):

```
RELEASE Qyl.Telemetry.SemanticConventions (3 NuGet packages + 1 npm package) <version> indexed <UTC time>
repo: <Qyl.Telemetry.AutoInstrumentation | qyl | qyl.mcp | qyl.at>
re-pin: <files from the table>
then: regenerate the lockfile, run the gate, commit, push main
```

This repo and AutoInstrumentation both feed qyl, which re-pins both in one commit once both are indexed: tell qyl's agent whether AutoInstrumentation moves in the same wave.
