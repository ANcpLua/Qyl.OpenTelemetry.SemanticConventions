# Qyl.Telemetry.SemanticConventions: release and pins

Use the existing clone on `main`, with one writer. Do not create an agent, branch, worktree or PR unless requested. Current owner instructions and verified source take precedence over historical plans. CI reports; use the repository’s actual release script and existing Vitals credentials.

## This repository

- Publishes four artifacts at one version: NuGet `Qyl.Telemetry.SemanticConventions`, `.Analyzers` and `.Incubating`, and npm `@ancplua/qyl-semantic-conventions` from `typescript/` (no runtime dependencies, no Zod).
- Version sources that agree on `main`: `Directory.Build.props` `VersionPrefix`, `registry/manifest.yaml` `schema_url` (`https://qyl.at/schemas/<x.y.z>`), `typescript/package.json` `version`, and a `## [x.y.z]` entry on top of `CHANGELOG.md` (the GitHub release notes). Run `scripts/generate.sh` after the bump and commit its output.
- qyl.at serves `https://qyl.at/schemas/<x.y.z>` before the push; `release.sh` creates and deploys it through the qyl.at clone (`../qyl.at` or `QYL_AT_DIR`) when missing.
- Keys: npm `~/.config/vitals/npm-qyl-manual-publish-token`; NuGet Vitals entry "qyl-dev manual publish and release (NuGet)", `~/.config/vitals/nuget-qyl-manual-publish-key`; read by the script. Never print, paste, commit or log a key. Entry missing, note expired, a rejected key or an npm one-time-password prompt: stop and tell the owner.
- Upstream pins: the OpenTelemetry registry inputs in `registry/manifest.yaml` and `scripts/fetch-core.sh`. When they move: `scripts/fetch-core.sh`, `scripts/generate.sh`, affected generator/consumer checks, commit, push `main`.

## Refresh changed inputs

1. Pull when the tree is clean and no other writer owns it. Install missing tools and restore only changed dependency inputs.
2. `VersionPrefix` is the last entry of `curl -fsSL https://api.nuget.org/v3-flatcontainer/qyl.telemetry.semanticconventions/index.json` or the next one; never reuse a published version.

## Release

1. Land the bump commit (version sources, regenerated output) on `main`.
2. `./release.sh <x.y.z>` (`--dry-run` first only when a step is new or unsure; the script refuses before publishing when anything is off). It returns before indexing, once NuGet and npm accepted the push and the tag and GitHub release exist; a detached job runs `./build.sh VerifyRelease --published-version <x.y.z>` (index wait for all four, release smoke against the registries) and then the snapshot refresh (`published-versions.yaml` in qyl.at), logging to `artifacts/release-<x.y.z>-verify.log`. Do not run the snapshot refresh by hand.
3. `./release.sh --wait <x.y.z>` follows that job until its log ends in VERIFIED or FAILED. One job owns the attempt. Continue independent work while it waits; do not restart a healthy job after a fixed interval or duplicate its registry polling. Exit 3 = not indexed in time, `./release.sh --verify <x.y.z>` checks again; any other FAILED after publication is a patch release.
4. VERIFIED: notify the consumers.

Stopped after NuGet accepted the packages: keep the four artifacts and `release-<version>.json`, renew credentials, wait until all three NuGet ids are indexed, then use `--resume` as the script header shows; a dry run is optional. Use an existing owner login for `--browser-auth` when needed; ask only for access or an interactive step that genuinely requires the owner.

## Consumers

| Consumer | Re-pins |
|---|---|
| Qyl.Telemetry.AutoInstrumentation | `Directory.Packages.props` (single source; the registry tag derives from it) |
| qyl | `Version.props` `QylSemanticConventionsVersion` |
| qyl.mcp | `server/package.json`, `workbench/package.json`, `bun.lock` |
| qyl.at | `src/data/site.ts` |

After VERIFIED, update authorized consumers in their existing clones, keeping package identity and deployment order intact. Coordinate with an existing writer when necessary; do not create an agent merely to notify a consumer.

This repo and AutoInstrumentation both feed qyl, which re-pins both in one commit once both are indexed: record whether AutoInstrumentation moves in the same wave.
