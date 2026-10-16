# SemanticConventions wave status

Version 11.0.0 is published and VERIFIED. The immutable release source is 2142f58; all three NuGet packages and the npm package passed their actual registry consumers. Native verification completed on 2026-10-16 at 16:41:50 UTC after 263 seconds of indexing. The matching schema is served at https://qyl.at/schemas/11.0.0 and the release script refreshed qyl.at's published-version snapshot.

The current source additionally updates only the test SDK dependency to OpenTelemetry 1.20.0. That SDK dependency is not shipped by the conventions packages, so this maintenance change does not replace or relabel 11.0.0. Its native `mise run check` passed all Python, regenerated-source, packed TypeScript, live-policy, solution-build and 241 MSTest checks. Evidence: artifacts/mstest-migration/otel120-native-check.log.

## Implemented

The stable and incubating surfaces still derive from registry 1.44.0 through Weaver 0.27.0. Runtime C#, private analyzer facts and the separate TypeScript catalog export derive from the same normalized registry facts. Registry lookups preserve value kinds, expanded template boundaries, units and exact lifecycle metadata. They do not invent scope membership, global requirements or closed enum sets. Exact multiline deprecation notes survive generation.

The canonical resource/scope/span records preserve typed values, schema URLs, flags, dropped counts, nanosecond timestamps, events and links. The four test-framework dependency leaks were removed at the shared Roslyn producer; native MSTest 4.5.1/MTP 2.5.1 runs on its public neutral 3.0.0 packages. QYL0200 accepts custom events, QYL0006 distinguishes actual resource schema metadata from ordinary attributes, and event/link collection analysis follows real SDK APIs.

The packed release consumers select both net10.0 and netstandard2.0 and verify nullable-flow metadata, generated APIs and registry lookups. The release-candidate full gate passed 241 tests and its unchanged 27%/23% coverage gates at 49.8% lines and 31.5% branches. That coverage run used SDK 1.19.1; the newer SDK check above is separate evidence.

## Inspection and limits

The complete INFO inspection is artifacts/mstest-migration/jb-catalog-final.xml, including style hints and the linked private catalog view. It is not a zero-finding report. A later correction marked the compiler-consumed nullable attribute explicitly and removed its redundant usage setting. Do not repeat the older pre-catalog claim of only three findings as the current state. Compiler namespaces, public Qyl.Spans layout and the internal analyzer copy of runtime metadata require source-aware assessment; no blanket suppression was added.

Weaver's installed live-check remains scope-blind for metric ownership and reports unregistered custom metric names as missing. The runtime catalog does not inherit those false global assumptions. Arbitrary conditional requirement text and enum membership are not claimed as fully evaluated.

## Consumers

AutoInstrumentation 26 is being integrated against public SemanticConventions 11 and API Contracts 14, with SDK/exporters 1.20.0. qyl's final pin must move with that producer because its gate checks the vocabulary compiled into AutoInstrumentation, not only package version labels. The MCP consumer has its public npm pins updated locally. The primary owns all remaining integration; all other agents are finished.
