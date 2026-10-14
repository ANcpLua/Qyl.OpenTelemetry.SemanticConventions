# Qyl.Telemetry.SemanticConventions

Release and re-pin: follow [`SKILL.md`](SKILL.md) at this repo root (canonical clone `/Users/alexandernachtmann/RiderProjects/Qyl.Telemetry.SemanticConventions/SKILL.md`); the one command is `./release.sh`. Published versions are read from the registries (nuget.org, npm) or `Directory.Build.props` `VersionPrefix`.

## Ownership and upstream versions

qyl owns and publishes these C# and TypeScript semantic-convention bindings. Weaver generates them directly from the language-independent OpenTelemetry registries plus qyl-owned and library-native definitions. Their update path is independent of upstream .NET SDK and automatic-instrumentation releases.

Read `registry/manifest.yaml` for the core and GenAI pins and `registry/vendor/` for the verified library versions. Compare those sources directly when auditing updates. An upstream .NET SDK or agent version does not establish this registry's coverage. Keep the language-independent convention version, qyl package version, vendor-library version and .NET SDK version distinct in reports.

## Publishing across the qyl-dev repos

Push to `main` directly; no pull request, review, approval or CI exists. The only gate is the repo's local command named in SKILL.md, run before the push.

A release is done from the developer's machine with the registry API keys registered in Vitals (`vitals keys`): bump the version source, pack, push the packages to the registry, then tag `v<version>` on the released commit and create the GitHub release. No workflow publishes.

| Repo | Version source | Publishes |
|---|---|---|
| qyl-api-schema | release tag (one version for npm and NuGet); `package.json` stays `0.0.0-development` on `main` | `@ancplua/qyl-api-schema` (npm), `Qyl.Api.Contracts` (NuGet) |
| SemanticConventions | `Directory.Build.props` `VersionPrefix`, `registry/manifest.yaml` `schema_url` and a `CHANGELOG.md` entry; `scripts/generate.sh` must agree; `qyl.at/schemas/<version>` is served first | the three `Qyl.Telemetry.SemanticConventions*` packages (NuGet) and `@ancplua/qyl-semantic-conventions` (npm) |
| AutoInstrumentation | `Directory.Build.props` `Version` | the six `Qyl.Telemetry.*` packages (NuGet) |
| qyl | `Version.props` `QylVersion` | `qyl`, `qyl.<rid>` ×6, `Qyl.Api.Sdk` (NuGet) |
| qyl.mcp | `server/package.json` `version` plus a `server/README.md` release-notes entry | `qyl-mcp-server` (npm) |
| qyl.at | none | `./release.sh` deploys `qyl.at` manually |

Downstream pins move in the change that consumes the publish: qyl-api-schema → qyl (`Version.props` `QylApiContractsVersion`, `services/qyl.dashboard/package.json`, `bun.lock`, the README table) → qyl.mcp (`server`, `workbench` and `dashboard` `package.json`, `bun.lock`); SemanticConventions → AutoInstrumentation (`Directory.Packages.props`, the single source; the registry ref derives from it) and qyl (`QylSemanticConventionsVersion`) and qyl.mcp (`server/package.json`, `bun.lock`), with qyl.at serving `public/schemas/<version>`; AutoInstrumentation → qyl (`QylTelemetryVersion`); every producer → qyl.at `src/data/site.ts`. One production rule stays: when the contract revision changes, push the Collector (qyl `main`) first, wait until `api.qyl.at/health` serves the new revision, then push qyl.mcp; `mcp.qyl.at` refuses a Collector on another revision.

Pushing `main` also deploys qyl → `qyl-collector` (`api.qyl.at`, Railway) and qyl.mcp → `qyl-mcp` (`mcp.qyl.at`, Railway). qyl.at deploys the `qyl-at` Worker (`qyl.at`, Cloudflare) manually through `./release.sh`.
