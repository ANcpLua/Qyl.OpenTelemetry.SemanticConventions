# Reference status

The upstream registry pins in [`Version.props`](../Version.props) are exact by design: a moving
input must not change generated constants without a commit in this repository. The scheduled
[`check_pin_freshness.py`](../src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/check_pin_freshness.py)
reports when a pin falls behind upstream; it does not decide whether to move one. Moving a pin is a
deliberate change: re-run
[`scripts/generate.sh`](../src/Qyl.Telemetry.SemanticConventions.SourceGeneration/scripts/generate.sh),
review the regenerated registry, and record the delta here.

Each entry names the pins before and after, the upstream commits the move absorbed, and what
changed in the generated surface — added, removed, renamed, or deprecated attributes, metrics,
events, spans and payload schemas. A removal is a public-API change for the published packages and
is called out as such.

## 2026-09-03 — `SemConvGenAiRef` `eaefa14` → `3bda576`

| Pin | Before | After |
|---|---|---|
| `SemConvSchemaVersion` | `1.44.0` | `1.44.0` (unchanged) |
| `SemConvGenAiRef` | `eaefa142a94cefe5d199d47e4a73727dfbd825df` | `3bda5760dcc5727a3c3ca4e898e79fad646988bc` |
| `WeaverVersion` | `0.25.1` | `0.25.1` (unchanged) |

The pinned GenAI manifest still declares `https://opentelemetry.io/schemas/1.44.0` as its core
dependency and still publishes `https://opentelemetry.io/schemas/gen-ai-dev/1.42.0-dev`, so
`generate.sh`'s core-dependency guard passes without touching `SemConvSchemaVersion`. Regeneration
is idempotent: a second run leaves no further diff.

### Upstream commits absorbed

open-telemetry/semantic-conventions-genai, `eaefa14..3bda576` (11 commits):

- `55a32cd` Add concise PR description and unslop skills (#465)
- `56d6b11` Exclude Slack workspace links from the link check (#467)
- `814aa0a` Upgrade conformance runner to add findings and entities support (#473)
- `5f5ae69` Remove cache token usage attributes from internal agent spans (#469)
- `110e8ca` Make reference scenario review instructions actionable (#455)
- `67dff02` Lock file maintenance (#456)
- `1fd1022` Lock file maintenance (#482)
- `5ca9052` gen-ai: deprecate per-message finish reason (#363)
- `ac46a5d` Update reference implementation dependencies (non-major) (#486)
- `ebf42b1` Update tooling dependencies (#491)
- `3bda576` Update tooling dependencies to v10 (#492)

Only three upstream model files moved: `model/gen-ai/registry.yaml`, `model/gen-ai/spans.yaml`, and
`model/gen-ai/gen-ai-output-messages.json`. The rest are repository tooling, CI, and lockfiles with
no projection into the registry.

### Generated-surface delta

**Nothing was added, removed, or renamed.** The merged catalog holds 980 attributes before and
after; groups 1287, entities 64, events 1, metrics 1, `scope_names` 6, `event_names` 3 — all
unchanged. Live-attribute counts are identical (`gen_ai.*` 72, `mcp.*` 4, `openai.*` 4,
`aws.bedrock.*` 2). **No generated constant disappeared, so this is not a public-API break for any
published package.**

Three substantive changes, everything else in the 8217-line `resolved-registry.json` diff being
per-row pin provenance (`source_ref`, `source_commit`, `source_date_epoch` on the 82 catalog rows
and 58 groups sourced from `genai`, 7944 of those lines):

- **`gen_ai.usage.cache_read.input_tokens` and `gen_ai.usage.cache_write.input_tokens` are no
  longer referenced by the `gen_ai.invoke_agent.internal` span** (upstream #469). Both were
  `recommended`, never required, on that one span. Both attributes remain in the catalog, remain
  `development`, remain undeprecated, and remain referenced by the inference spans — so
  `SemconvRegistryFacts.g.cs` and the generated constants are byte-identical apart from the
  provenance header, and `QYL0401` (missing required GenAI attributes) is unaffected.
- **The per-message `finish_reason` field of the `gen_ai.output.messages` payload schema is
  deprecated** (upstream #363). In the shipped
  `Qyl.Telemetry.SemanticConventions.Incubating/schemas/gen-ai/gen-ai-output-messages.json` the
  field leaves the `OutputMessage` `required` list, gains `"deprecated": true`, `"default": null`
  and a `null` member in its type union, and its description now redirects to
  `gen_ai.response.finish_reasons`. This relaxes the payload schema — a document that omits
  `finish_reason` now validates, and one that carries it still does.
- **`gen_ai.response.finish_reasons` gains a note and a third example.** The note fixes the array's
  contract: values correspond to generations in the order the provider returned them, and a
  position whose finish reason never arrived (failure, cancellation, a stream that ended early)
  SHOULD report `error`. The examples gain `["stop", "length", "error"]`. `gen_ai.output.messages`
  gains a matching note sentence saying `gen_ai.response.finish_reasons` tracks the provider's
  generations, not a filtered or truncated message list. Type (`string[]`), stability
  (`development`), and brief are unchanged, so no constant or analyzer fact moves.

### Shipped generated code

All 136 files the package projections emit were rendered under both pins and compared. The file set
is identical and every member name is identical (`GenAiAttributes` 124 members, `McpAttributes` 31,
`OpenaiAttributes` 11, all others byte-identical). Three files change at all:

- `GenAiAttributes.g.cs` — the provenance header sha, plus the two doc-comment additions above.
- `McpAttributes.g.cs`, `OpenaiAttributes.g.cs` — the provenance header sha only.

Two pinned baselines move with the regeneration, both because of the header sha and the doc
comments, neither because a member changed:

- `tests/.../Snapshots/qyl.package.manifest.sha256` — three of its 131 lines, regenerated with
  `REGEN_SNAPSHOTS`. No line added or removed.
- `RegistryShapeGateTests.Root_records_all_three_registry_sources_and_the_upstream_manifests` —
  the genai `source_commit` it asserts, which pins the ref by construction.

The full-file snapshots (`qyl.package.attributes.http.*`, `qyl.package.attributes.qyl.incubating`,
`qyl.package.names`, `qyl.package.schemaurl`) are untouched: the stable tier does not move.

### Gates

`dotnet build Qyl.Telemetry.SemanticConventions.slnx -c Release` succeeded at 0 warnings, 0 errors.
Pipeline tests 73/73, source-generation tests 81/81. Regeneration is idempotent on a second run.

`check_pin_freshness.py` now reports `SemConvSchemaVersion` and `SemConvGenAiRef` current. It also
reports `WeaverVersion` behind: upstream released weaver `v0.26.0` after issue #37 was filed, which
recorded `0.25.1` as current. That pin is deliberately **not** moved here — a Weaver bump re-renders
every projection and is its own reviewed change.
