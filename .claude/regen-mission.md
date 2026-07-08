# MISSION: regenerate the full 1.43.0 attribute surface (fix stale-attribute-surface-1430)

Read memory `stale-attribute-surface-1430.md` first — it has the full finding.
Short version: commit `875c29a` regenerated ONLY `GenAiAttributes.g.cs`; every
other `Attributes/*.g.cs` in both packages is a May-24 (~1.41-era) fossil while
`SchemaUrl.g.cs` claims 1.43.0. The correct data already sits in
`src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/Resources/resolved-registry.json`
(core 1.43.0 + genai registry, verified correct).

## Ground rules

- Main loop: **Sonnet** (`/model sonnet`). Spawn workflow agents with
  `model: 'sonnet'` explicitly — never let them inherit a pricier model.
- Use the `fable-advisor` agent for judgment calls ONLY (emitter edge cases,
  ambiguous conventions). One tight question per call, few calls total.
- Deterministic emitter > LLM-written files. Agents write and verify the
  emitter; only the script writes `.g.cs` files. No agent hand-writes
  generated code.
- Commit straight to main, push when green (user's standing contract).

## Phase 1 — write the emitter (1 agent, careful)

Write `src/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/scripts/emit_attributes.py`:
reads `Resources/resolved-registry.json`, emits per-root-namespace
`{Group}Attributes.g.cs` files for BOTH packages:

- stable package (`src/Qyl.OpenTelemetry.SemanticConventions/Attributes/`):
  include rows with `stability == "stable"` PLUS deprecated rows
  (mirrors `StabilityFiltering.IsIncludedOrDeprecated` with StableOnly —
  read `SourceGeneration/Extractors/StabilityFiltering.cs`).
- incubating package (`…SemanticConventions.Incubating/Attributes/`): all rows.
- Conventions to reproduce EXACTLY (study `GenAi/GenAiAttributes.g.cs` for
  fresh shape and `Net/NetAttributes.g.cs` for deprecated shape):
  PascalCase const names, nested `…Values` classes for well-known values,
  `[global::System.Obsolete("Replaced by X.", false)]`, XML doc comments from
  brief/note with `<c>…</c>`, file-scoped namespaces
  `Qyl.OpenTelemetry.SemanticConventions[.Incubating].Attributes.{Group}`.

## Phase 2 — validate the emitter (BLOCKING gate)

1. **Byte-reproduce `GenAiAttributes.g.cs`** from the current JSON. Any diff =
   emitter wrong (or ad-hoc quirk in the checked-in file — ask fable-advisor
   which). Do not proceed until byte-identical or every diff is explained.
2. Bonus check: `git show 1fc1313:src/…/Resources/resolved-registry.json`
  (or nearest historical revision) → emitter over old JSON should
   approximately reproduce the stale files; investigate structural diffs.

## Phase 3 — regenerate + fan-out verify (the fleet part)

1. Delete both `Attributes/` trees, run the emitter, regenerate everything.
2. Fan out verification agents (sonnet, one per attribute group, ≤50 in
   flight): each agent gets one emitted file + the matching JSON slice and
   checks: every stable attr present in stable pkg, no development attr in
   stable pkg (deprecated allowed), Obsolete messages match deprecation info,
   k8s/container stable graduations present, `gen_ai.*` unchanged
   byte-for-byte. Report per-group verdicts; fix root causes in the emitter
   (never patch individual files) and re-run.

## Phase 4 — gates + ship

1. `dotnet build Qyl.OpenTelemetry.SemanticConventions.slnx` → must be 0/0
   (TreatWarningsAsErrors; note the NoWarn CS15xx band already covers doc-ref
   warnings).
2. Tests are MTP exe-style: `dotnet run --project tests/<proj>.csproj -c Debug`
   for SourceGeneration.Tests and Pipeline.Tests. Snapshots may legitimately
   change — update snapshots only when the diff is explained by the regen.
3. `./build.sh SeedAttributesHash` (the hash MUST be reseeded after regen or
   CI's VerifyAttributesHash goes red — see memory `qyl-semconv-version-state`).
4. Single consolidated commit to main + push. Then bump `VersionPrefix` in
   `Directory.Build.props` to 3.3.0, update the AGENTS.md version paragraph,
   tag `v3.3.0`, push tag (workflow publishes all five packages).
5. Update memory: mark `stale-attribute-surface-1430.md` resolved with the
   commit/tag; keep the emitter path documented in AGENTS.md so the NEXT
   version bump regenerates the whole surface, not one file.

## Success criteria

Stable package gains K8s/Container groups (k8s.cluster.name, k8s.namespace.name,
k8s.pod.uid, container.id, …); incubating gains 1.42/1.43 additions
(browser.document.url.full, service.criticality, v8js heap.space.size, …);
GenAi byte-identical; build 0/0; both test suites green; hash reseeded;
v3.3.0 tagged and published.

## Final acceptance gate — the mask-detection probes (ALL must flip)

Labels/hashes/green CI verify consistency, not truth. A regen is only real if
the diff it implies exists. Run all five; a "masked" result on any = mission
NOT done, fix the emitter (never hand-patch files):

1. **Birth probe** — keys that didn't exist pre-1.42 must now exist:
   `grep -r "browser.document.url.full\|service.criticality" src/*/Attributes/ --include="*.cs"`
   → must hit in Incubating (today: zero hits).
2. **Migration probe** — 1.42 graduations must appear in STABLE:
   `ls src/Qyl.OpenTelemetry.SemanticConventions/Attributes/ | grep -E "K8s|Container"`
   and `grep -rn "k8s.namespace.name" src/Qyl.OpenTelemetry.SemanticConventions/Attributes/`
   → both must hit (today: neither does).
3. **Death probe** — 1.42 renames must show old+Obsolete and new:
   `grep -rn "heap.space.size\|heap.limit" src/*/Attributes/V8js/ --include="*.cs"`
   → new `heap.space.size` const present; old semantics not silently kept.
4. **Reconciliation probe** — count registry stable rows vs stable-package
   consts (script in the session notes); residual must be EXPLAINED
   (deprecated-stays, value consts), not waved at.
5. **Forensic probe** — `git show --name-only <regen-commit> | grep -c "Attributes/"`
   → ~100, not 1 (how the stale surface was caught: 875c29a touched exactly 1).
