# qyl — Vendor-Neutral Zero-Code .NET OpenTelemetry Instrumentation Runtime

**Status:** Design (brainstorming output) · **Date:** 2026-05-29
**Authors:** ancplua + Claude (Opus 4.8)
**Supersedes:** none · **Feeds:** `/ultraplan` of Milestones M0 + M1

> ⚠️ This design doc lives in the `SemanticConventions` repo only because that is
> the current working context. qyl is a **separate** runtime; relocate this spec
> into the qyl repo once that repo is the working context. Nothing here ships in
> the four `Qyl.OpenTelemetry.SemanticConventions*` packages.

---

## 1. Mission

Build the **best open-source, free, vendor-neutral .NET zero-code OpenTelemetry
instrumentation runtime** — the thing OpenTelemetry intends the community to
build. It attaches to an **unmodified process** and, via a **thin native CLR
profiler** (attach + IL rewrite + ReJIT) plus a **large managed C# layer that
owns all behavior**, emits OTel traces / metrics / logs (profiles-ready),
**reusing the OpenTelemetry .NET SDK** and the `Qyl.OpenTelemetry.SemanticConventions`
generator.

This is a mission, not a marketing product. Success = OTel-faithful, correct,
and good enough that it could be upstreamed into `opentelemetry-dotnet-instrumentation`.

### Non-negotiable properties
- **Vendor-neutral.** OTLP-first. No Datadog/vendor-specific coupling anywhere.
- **Zero-code.** No application source changes (build-time NuGet / env activation allowed).
- **Reuse, don't reinvent.** OTel SDK, exporters, and contrib source-instrumentations are reused as-is.
- **SemConv-correct by construction.** Every attribute name flows from the generated dictionary.

---

## 2. Verified alignment with OpenTelemetry's stated intent

Confirmed against `opentelemetry-dotnet-instrumentation/docs/design.md` (read 2026-05-29):

| OTel `design.md` says | qyl design |
| --- | --- |
| "instrument applications without changing the source code" | core mission |
| startup hook injects the SDK before app code; CLR Profiler enables bytecode instrumentation + is required on .NET Framework | exact "thin native + fat managed" split |
| two instrumentation types: **source** (API hooks) + **bytecode** (IL rewrite) | M1 (source) → M2 (bytecode) |
| "Errors at runtime are logged and should **never crash the application**" | supreme invariant (§6), gates every milestone |
| Vision: high-performance, reliable, useful-by-default, extensible | adopted verbatim |
| Unsupported: AOT; side-by-side CLR profilers | honest non-goals (§9) |

**The most OTel-intended posture is reuse + upstream, not a divergent fork.** qyl
reuses the OTel SDK/contrib; its two differentiators (generated-SemConv constants,
MCP instrumentation) are designed to be upstreamable.

---

## 3. Definition of "Complete" (honest)

For code that rewrites IL inside a customer's live process, "100% complete" has
exactly one honest meaning, and this project commits to it:

> **Total coverage** — every box in the north-star blueprint (Appendix A) maps to
> a milestone; nothing is silently dropped — **plus per-milestone correctness gates**:
> each milestone passes **golden-OTLP**, **no-behavior-change**, and **SemConv
> conformance** before the next begins.

Complete = *proven, milestone by milestone*. Not "all task chains delivered at once."
Any coverage that is bounded (sampled libraries, deferred signals) is explicitly
logged as a non-goal or a later milestone — never left implicit.

---

## 4. Architecture

### 4.1 Language-ownership boundary
- **C# owns ALL behavior**: configuration, provider bootstrap, instrumentation
  logic, semantic mapping, redaction, sampling, context propagation, export.
- **Native (C++) owns ONLY the CLR-attachment mechanics it must**: COM entrypoint,
  `ICorProfilerCallback*`/`ICorProfilerInfo*`, `SetEventMask`, module/method
  discovery, `RequestReJIT`/`GetReJITParameters`, `Get/SetILFunctionBody`,
  metadata import/emit, crash-containment, per-bitness binaries, OS injection glue.
- **C# must NOT own**: raw runtime-callback lifetime, unmanaged payload memory,
  metadata ABI correctness, JIT/ReJIT reentrancy, process-crash-safe native error
  handling, cross-platform native loader semantics.
- **Speculative, OFF the critical path**: a NativeAOT/C# COM profiler shell. The
  COM/vtable boundary favors C++ (proven). Revisit only if independently proven.

### 4.2 Layers
```
AttachmentLayer   → launch/service/IIS/container/k8s/cloud attach; startup-hook; profiler; dynamic
NativeBoundary    → COM server, callback router, event mask, module/metadata/IL/ReJIT bridges
BootstrapLayer    → StartupHook → Loader (isolated ALC) → ManagedProfiler activation → conflict resolve
ManagedRuntime    → Config · ResourceDetection · ProviderBootstrap · Propagation · Sampling · Export · Shutdown · Suppression
InstrumentationLayer → source + bytecode instrumentations, CallTarget handlers, semantic/error/redaction mappers
RuleLayer         → generated + manual rules; version/runtime/platform/signal/safety/stability matchers
SemConvLayer      → generated stable/experimental constants, deprecated redirects, requirement-level routing, conformance manifest
ValidationLayer   → unit · native smoke · IL/ReJIT · golden-OTLP · SemConv-conformance · no-behavior-change · perf · fuzz
```

### 4.3 Data flow (zero-code, modern .NET)
1. Activation sets env (`CORECLR_ENABLE_PROFILING`, `DOTNET_STARTUP_HOOKS`, `OTEL_*`).
2. StartupHook runs before `Main`; Loader sets up an isolated `AssemblyLoadContext`.
3. ManagedProfiler builds Tracer/Meter/Logger providers (reused OTel SDK) → OTLP exporter.
4. Source instrumentations subscribe (DiagnosticListener/ActivitySource/MeterListener).
5. For libraries without hooks, the native profiler ReJITs target methods and
   injects `CallTarget.BeginMethod/EndMethod` calls into managed handlers.
6. Handlers set attributes **from the generated SemConv dictionary** and emit OTLP.

---

## 5. Reuse + Upstream policy

**Reuse as-is (equal-or-better exists):** `OpenTelemetry.Api/Sdk`, OTLP exporter,
`Extensions.Hosting`, `Resources`, Trace/Metrics/Logs, and contrib source
instrumentations (AspNetCore, Http, GrpcNetClient, SqlClient, EFCore,
StackExchangeRedis, Runtime, Process). Reference `opentelemetry-dotnet-instrumentation`
as a fork/reference for the native profiler + CallTarget/IL/ReJIT mechanics.

**Wrap (BCL primitives):** `Activity*`, `Meter`/instruments, `ILogger`,
`DiagnosticSource`/`EventSource`, `AssemblyLoadContext`.

**Write ourselves (the moat):** attachment orchestrator, isolated loader, managed
profiler bootstrap, bytecode rule compiler, CallTarget ABI wrappers, duck typing,
**SemConv-generated constants + conformance manifest**, redaction engine, safety
gates, golden-OTLP + no-behavior-change harness.

**Differentiators to upstream:** (1) generate SemConv constants from the registry
instead of hand-typed strings (replacing the engine's `*Attributes.cs`);
(2) **MCP instrumentation** (no existing engine ships it).

---

## 6. Correctness invariants (gates across ALL milestones)
1. **Never crash the app.** No instrumentation exception escapes; no app exception swallowed.
2. **Fail-closed on profiler conflict** (only one CLR profiler may attach) — day-1 gate.
3. **Fail-open on instrumentation error** — disable the offending instrumentation, keep running.
4. **No behavior change** — return values and exception flow preserved exactly (golden tests).
5. **Sensitive data off by default** — headers allowlist-only; SQL/GraphQL/GenAI/payload capture opt-in.
6. **SemConv conformance** — each instrumented operation emits its `requirement_level: required` subset, all dictionary-named.
7. **Golden OTLP** — every instrumentation has a recorded golden output diffed in CI.

---

## 7. Milestone spine

Each milestone is **independently correct and shippable**. Gates from §6 apply throughout.

| M | Goal | Exit criteria |
| --- | --- | --- |
| **M0** | Baseline + repo + SemConv generation + golden-OTLP harness | target matrix frozen; generator wired; golden + no-behavior-change harness runs green on an empty app |
| **M1** | Managed spine, **no native yet** | StartupHook→Loader→Config→Resource→Providers→OTLP working; source instrumentation for HttpClient + ASP.NET Core; a **real zero-code tracer** on net8/9/10 |
| **M2** | Thin native profiler | attach + module/method discovery + ReJIT + IL rewrite + NativeBridge + CallTarget ABI + DuckTyping; bytecode instrumentation + .NET Framework path |
| **M3** | Breadth | DB · messaging · cache · RPC · cloud · logging |
| **M4** | Differentiators | GenAI · **MCP** · feature-flags · exceptions |
| **M5** | Hardening | full safety invariants · OS/TFM matrix · installers · profiles-ready |

M1 ships a working artifact before any C++ exists — this is the anti-vaporware mechanism.

---

## 8. M0 + M1 detail (first `/ultraplan` scope)

### M0 — Baseline & harness
- Verify/freeze: OTel SemConv version (1.41.x), OTel .NET auto-instr reference
  version, runtime matrix (net462+, net6–net10), profiler API surface, startup-hook
  behavior, OS/arch matrix.
- Repo bootstrap: solution, projects, deterministic build, artifacts layout, CI matrix.
- SemConv generation: consume the `Qyl.OpenTelemetry.SemanticConventions` generator;
  produce stable + experimental constants, deprecated redirects, schema-URL map,
  **requirement-level metadata**, and the **conformance manifest** (per group →
  required/recommended/opt-in attribute sets).
- Validation harness: golden-OTLP recorder/differ + no-behavior-change scaffold.

### M1 — Managed zero-code tracer
- `AutoInstrumentation.StartupHook` (`public static void Initialize()`), idempotent.
- `AutoInstrumentation.Loader`: isolated ALC, assembly-conflict detection, diagnostics.
- `Configuration`: env + file + precedence + validation + redacted dump + effective config.
- `ResourceDetection`: service/host/os/process/runtime/container/k8s/cloud + `OTEL_RESOURCE_ATTRIBUTES` merge.
- `ProviderBootstrap`: Tracer/Meter/Logger providers via reused OTel SDK; OTLP http+grpc exporters; propagators; samplers.
- Source instrumentation: **HttpClient** + **ASP.NET Core** (reuse contrib), wired through qyl's source-runtime lifecycle and suppression.
- **Exit:** `OTEL_TRACES_EXPORTER=otlp dotnet run` on an untouched sample API emits
  spec-conformant `http.server.request` + `http.client` spans, golden-OTLP green,
  no-behavior-change green, conformance green.

---

## 9. Honest non-goals / boundaries
- **AOT-compiled apps unsupported** (no JIT/startup-hook to hook) — emit an explicit marker.
- **Single CLR profiler only** — detect & fail-closed on side-by-side profilers.
- **"Unmodified process" asterisk** — modern .NET may require a startup-hook env / NuGet;
  truly env-var-only attach is the profiler path. State this precisely; never oversell.
- **Profiles** are placeholder/ready (pprof mapping deferred to post-M5).
- **NativeAOT/C# profiler** is speculative, off the critical path.

---

## 10. Open decisions to lock during `/ultraplan`
1. qyl runtime repo location (separate repo in `O-ANcppLua`; this spec relocates there).
2. Native route: from-scratch C++ vs. fork `opentelemetry-dotnet-instrumentation` native vs. optional CLRIE cooperation on Windows.
3. .NET Framework priority (M2 vs. later) — drives how early the profiler is mandatory.
4. Initial OS/arch slice for M2 (recommend linux-x64 + win-x64 first).
5. Packaging/attach surface for v0.1 (CLI wrapper vs. NuGet vs. install script).

---

## Appendix A — North-star coverage map (the blueprint)

The full `AUTOINSTRUMENTATION.BLUEPRINT.NET.OTEL.2026` is the authoritative
coverage checklist. Mapping of its sections → milestones (so nothing is dropped):

| Blueprint section | Milestone(s) |
| --- | --- |
| `00` language ownership, `01` reuse decision | M0 (policy) |
| `02` repo skeleton, `T002` bootstrap | M0 |
| `T003` SemConv generation, `06` full coverage registry | M0 (generation) + gates everywhere |
| `T005`–`T009` startup/loader/config/resource/providers | M1 |
| `T012` source runtime, `T014`–`T015` HTTP server/client | M1 |
| `04` NativeBoundary, `T004`/`T011`/`T013` profiler/CallTarget/IL/ReJIT | M2 |
| `T016`–`T025` RPC/DB/cache/messaging/cloud/logging/runtime | M3 |
| `T026`–`T029` feature-flags/GenAI/**MCP**/exceptions, `07` domain modules | M4 |
| `T030` profiles, `T031` security/safety, `T032` test matrix, `08` golden shapes, `09` done-state | M5 (+ gates from M0) |

Coverage registry (`06`) — CoreCommon, ResourceEntity, DotNet, HTTP, RPC, Database,
Messaging, GenAI, MCP, GraphQL, FaaS, ObjectStores, System, URL, etc. — is validated
continuously via the conformance manifest, not emitted wholesale.
