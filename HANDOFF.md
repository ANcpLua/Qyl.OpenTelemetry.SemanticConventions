# SemanticConventions 11.0.0 candidate handoff

This checkout contains the canonical OTLP trace snapshot changes, analyzer corrections and MSTest migration for the coordinated major wave. Publication is held: the primary agent will add and review a generated registry facts catalog before releasing 11.0.0 once. The package version remains independent of OpenTelemetry registry 1.44.0, protocol 1.11.1 and SDK 1.19.1.

## Public contract and rule changes

`SpanRecord(traceId, spanId, name)` has named initialization for metadata. Resource and instrumentation scope have independent schema URLs, typed attributes and dropped counts. Trace/event/link flags, trace state, dropped counts, resource entity references and exact nanosecond times are retained. Handwritten vocabulary types now live directly under the stable project directory; their public namespaces are unchanged.

QYL0200 reserves qyl-owned names without rejecting arbitrary custom OpenTelemetry names. QYL0006 reports the misleading ordinary resource attribute `telemetry.schema_url` and points to the SDK's real `schemaUrl` parameter; it does not guess or invent a schema. QYL0201 is deleted. QYL0402 targets actual `Meter.CreateHistogram` calls. QYL0202 no longer recognizes nonexistent attribute APIs. Real SDK fixtures replace fake producer APIs.

Analyzer test runners fail on Roslyn AD engine diagnostics before selecting rule diagnostics. A deliberately crashing owned analyzer proves this cannot become an empty successful result. Real event/link tag collections are traversed, and migration catalog replacement arrays use structural equality in dictionaries and sets.

## Verification and boundaries

The fresh public-dependency native Fallout Coverage run with .NET SDK 10.0.401, MSTest 4.5.1, MTP 2.5.1 and DotCov 4.7.0 passes 228 cases with no failures or skips. The final run measures 8380/19119 lines (43.83%) and 1653/5285 branches (31.28%); the unchanged 27% line and 23% branch gates pass. Generated sources are included. The existing source boundary is this repository; moving the isolated NuGet cache outside that boundary prevents embedded dependency sources being mistaken for repository sources. No coverage exclusions or threshold reductions were added, and the report's excluded-file list is empty.

Evidence: `artifacts/mstest-migration/coverage-public.json`, `coverage-public-unfiltered.json`, `public-coverage.log` and `public-check-final.log`; Python 28/28 and packed TypeScript 1/1 checks pass. The complete native `mise run check` passed, including generated-vs-HEAD reproduction, live-check policy fixtures and all 228 MSTest cases. Documentation generation and its explicit checks pass. The test review at `artifacts/mstest-migration/test-review.json` covers 147 method bodies, 32 class fixtures and three owned helpers: 102 A and 45 B method grades, all passing; no mutation test was run.

InspectCode 2026.2.3.1 has three remaining namespace-layout findings: the compiler-required `System.Runtime.CompilerServices.IsExternalInit` namespace, and the public `Qyl.Spans` partial namespace reported for both target frameworks. Actual implementation findings are fixed; these intended compiler/public contracts are retained without blanket suppression. Evidence: `artifacts/mstest-migration/jb-inspections-current.xml`.

## Producer publication and remaining gates

ANcpLua.Roslyn.Utilities 3.0.0 is published through its native CI from commit `5e33da5c0f9dd37397dda3024e13f3e587b750de`: [CI 38923866011](https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities/actions/runs/38923866011), [v3.0.0](https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities/releases/tag/v3.0.0). All thirteen package pushes succeeded and all thirteen versions are publicly indexed. The required public package payloads match the native CI artifacts apart from NuGet signatures (`public-payload-comparison.json`). Testing is framework-independent; opt-in Xunit, NUnit, TUnit, Bunit and MSBuild integrations are separate packages. This consumer pins the public neutral Testing and Sources 3.0.0 packages; no candidate feed is committed.

The first public-only restore failed NU1103 while NuGet indexed the new release. Sources became visible at 15:46:46 UTC on 16 October 2026; the subsequent isolated restore succeeded from nuget.org alone. `public-dependency-graph.json` and `public-package-metadata.json` record the installed Testing/Sources 3.0.0 provenance and the absence of xUnit, NUnit and TUnit. The complete native gate and fresh public-dependency coverage passed. These source checks do not constitute a SemanticConventions publication. Candidate packages used during development came from the producer's isolated local feed; that feed was removed from `nuget.config` before the candidate commit.

## Explicit remaining leads for the primary agent

There is no public registry-wide attribute/type/deprecation catalog today. Per-family definitions and caller-selected span conformance are valid; reflecting over constants or applying all required attributes globally is not. The primary agent owns the proposed generated catalog and final release.

The installed pinned Weaver 0.27.0 binary proves registered metric unit and instrument mismatches, but its live-check report drops supplied instrumentation scope metadata. Its source checkout has later scope support; those are different artifacts. Controlled evidence is retained in `metric-policy-samples.json`, `metric-policy-report.json` and `metric-policy.stderr`. Unknown custom metrics currently produce `missing_metric` findings. No scope-blind duplicate-name rule was added.

Roslyn Utilities' publish workflow still has one old comment saying `auto-bump-on-merge`; its actual validated release path uses the explicit `ReleaseVersion` in Version.props. Correct that comment with its next deliberate version change, rather than triggering an unnecessary new publication for a comment alone.

Official SDK wire proof in the separate Qyl.Telemetry.Testing candidate passes 2/2 for absent versus explicit resource schema headers and an independent misleading attribute (`artifacts/schema-wire-tests.log` in that repository). This repository's SDK export tests also assert those values. The testing package remains a separate producer/consumer responsibility.
