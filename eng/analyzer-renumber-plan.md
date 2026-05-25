# Analyzer ID Renumber Plan (3.0.0 Break)

Source of truth for the renumber: `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/AnalyzerReleases.Shipped.md` (153 entries — 48 active rules + 105 reserved-disabled catalog stubs).

This document is a planning artifact only. No source files are modified.

---

## 1. Domain blocks (proposed)

Eight 100-wide bands. The widest active band carries 13 rules, leaving ~85 free slots per band for growth — no future re-renumber required.

| Range            | Domain                                              | Rules in band |
|------------------|-----------------------------------------------------|---------------|
| `QYL0001..0099`  | **Semantic conventions** (catalog migration, deprecated members/values, opt-in, library hygiene, attribute type/value validation, schema URL) | 13 |
| `QYL0100..0199`  | **Tracing** (Activity, ActivitySource, `[Traced]`, `[TracedTag]`, `[NoTrace]`, span shape, exception recording) | 11 |
| `QYL0200..0299`  | **Metrics** (Meter registration, instrument naming, tag cardinality) | 3 |
| `QYL0300..0399`  | **Configuration / setup** (ServiceDefaults, collector endpoint, OTel composition, resource attributes, EventSourceSupport, legacy accessor types) | 6 |
| `QYL0400..0499`  | **GenAI** (`gen_ai.*` semconv, agent tracing, direct SDK usage, token-usage histogram, operation names) | 7 |
| `QYL0500..0599`  | **Source generator / partial enforcement** (`[Meter]`, `[Counter]/[Histogram]` partial requirements) | 2 |
| `QYL0600..0699`  | **Security / sensitive data** (insecure endpoint, PII/credential leakage) | 2 |
| `QYL0700..0799`  | **Export / batching / performance** (OTLP endpoint, compression, SimpleSpanProcessor, sampling) | 4 |

Reservations not used: `QYL0800..0999` left empty for future bands (e.g., logs/events, AOT/trim, attribute conventions).

Within each band, new IDs are assigned by ascending old ID — deterministic and easy to verify mechanically.

---

## 2. Old to New ID mapping

48 active rules. Each row sourced from `AnalyzerReleases.Shipped.md`, with title text shortened from the descriptor's `title:` field (or the shipped.md Notes column when no descriptor exists outside the analyzer file).

| Old ID  | New ID  | Title                                                                                       | Domain band     | Has analyzer file? | Has resx key? |
|---------|---------|---------------------------------------------------------------------------------------------|-----------------|---------------------|----------------|
| QYL0002 | QYL0001 | graphql.document is opt-in                                                                  | Semantic conv.  | yes (`GraphqlDocumentOptInAnalyzer.cs`)                          | no (AL0002 only) |
| QYL0005 | QYL0002 | RPC server span must not include client.address / client.port                               | Semantic conv.  | yes (`RpcServerClientAttributeAnalyzer.cs`)                      | no (AL0005 only) |
| QYL0010 | QYL0003 | Deprecated semantic-convention constant                                                     | Semantic conv.  | yes (`DeprecatedSemconvAnalyzer.cs`)                             | no (AL0010 only) |
| QYL0011 | QYL0004 | Prefer typed semantic-convention constant over string literal                               | Semantic conv.  | yes (`PreferSemconvConstantAnalyzer.cs`)                         | no (AL0011 only) |
| QYL0012 | QYL0005 | String literal matches a deprecated semantic-convention name                                | Semantic conv.  | yes (`LiteralMatchesDeprecatedSemconvAnalyzer.cs`)               | no (AL0012 only) |
| QYL0013 | QYL0006 | Missing telemetry schema URL                                                                | Semantic conv.  | yes (`QYL0013MissingSchemaUrlAnalyzer.cs`)                       | yes (AL0013) |
| QYL0014 | QYL0007 | Deprecated semantic-convention value                                                        | Semantic conv.  | yes (`DeprecatedSemconvValueAnalyzer.cs`)                        | no (AL0014 only) |
| QYL0021 | QYL0008 | Incubating semantic-convention member used in a library                                     | Semantic conv.  | yes (`IncubatingSemconvInLibraryAnalyzer.cs`)                    | no |
| QYL0030 | QYL0009 | Obsolete semantic convention has an exact replacement                                       | Semantic conv.  | yes (`SupplementalSemconvMigrationAnalyzer.cs`)                  | no |
| QYL0031 | QYL0010 | Semantic convention migration needs review                                                  | Semantic conv.  | yes (`SupplementalSemconvMigrationAnalyzer.cs`)                  | no |
| QYL0032 | QYL0011 | Legacy semantic convention appears in compatibility or test code                            | Semantic conv.  | yes (`SupplementalSemconvMigrationAnalyzer.cs`)                  | no |
| QYL0085 | QYL0012 | Invalid attribute value (violates OTel semconv specification)                               | Semantic conv.  | yes (`QYL0085InvalidAttributeValueAnalyzer.cs`)                  | yes (AL0085) |
| QYL0086 | QYL0013 | Incorrect attribute type                                                                    | Semantic conv.  | yes (`QYL0086IncorrectAttributeTypeAnalyzer.cs`)                 | yes (AL0086) |
| QYL0061 | QYL0100 | Activity/Span missing semantic convention attributes                                        | Tracing         | yes (`QYL0061ActivityMissingSemconvAnalyzer.cs`)                 | yes (AL0061) |
| QYL0063 | QYL0101 | Unregistered ActivitySource (missing AddSource)                                             | Tracing         | yes (`QYL0063UnregisteredActivitySourceAnalyzer.cs`)             | yes (AL0063) |
| QYL0073 | QYL0102 | `[Traced]` requires non-empty ActivitySourceName                                            | Tracing         | yes (`QYL0073TracedActivitySourceNameAnalyzer.cs`)               | yes (AL0073) |
| QYL0077 | QYL0103 | Duplicate instrumentation (auto + manual span)                                              | Tracing         | yes (`QYL0077DuplicateInstrumentationAnalyzer.cs`)               | yes (AL0077) |
| QYL0078 | QYL0104 | Invalid ActivitySource name (reverse-DNS)                                                   | Tracing         | yes (`QYL0078InvalidActivitySourceNameAnalyzer.cs`)              | yes (AL0078) |
| QYL0079 | QYL0105 | Complex async pattern recommends manual span                                                | Tracing         | yes (`QYL0079ManualSpanRecommendedAnalyzer.cs`)                  | yes (AL0079) |
| QYL0107 | QYL0106 | `[TracedTag]` on parameter where neither method nor type has `[Traced]`                     | Tracing         | yes (`QYL0107OrphanedTracedTagAnalyzer.cs`)                      | yes (AL0107) |
| QYL0108 | QYL0107 | `[NoTrace]` on a method whose declaring type has no class-level `[Traced]`                  | Tracing         | yes (`QYL0108RedundantNoTraceAnalyzer.cs`)                       | yes (AL0108) |
| QYL0109 | QYL0108 | `[Traced]` on abstract/extern/partial method (non-interceptable)                            | Tracing         | yes (`QYL0109NonInterceptableTracedAnalyzer.cs`)                 | yes (AL0109) |
| QYL0110 | QYL0109 | `[TracedTag]` on out/ref parameter                                                          | Tracing         | yes (`QYL0110TracedTagOnOutRefParameterAnalyzer.cs`)             | yes (AL0110) |
| QYL0113 | QYL0110 | `Activity.SetStatus(Error)` inside catch without RecordException                            | Tracing         | yes (`QYL0113MissingExceptionRecordingOnActivityAnalyzer.cs`)    | yes (AL0113) |
| QYL0067 | QYL0200 | Unregistered Meter (missing AddMeter)                                                       | Metrics         | yes (`QYL0067UnregisteredMeterAnalyzer.cs`)                      | yes (AL0067) |
| QYL0068 | QYL0201 | Invalid metric instrument name                                                              | Metrics         | yes (`QYL0068InvalidMetricNameAnalyzer.cs`)                      | yes (AL0068) |
| QYL0075 | QYL0202 | High-cardinality metric tag                                                                 | Metrics         | yes (`QYL0075HighCardinalityMetricTagAnalyzer.cs`)               | yes (AL0075) |
| QYL0069 | QYL0300 | Incomplete ServiceDefaults configuration                                                    | Configuration   | yes (`QYL0069IncompleteServiceDefaultsAnalyzer.cs`)              | yes (AL0069) |
| QYL0070 | QYL0301 | Non-OTLP collector endpoint                                                                 | Configuration   | yes (`QYL0070NonOtlpCollectorEndpointAnalyzer.cs`)               | yes (AL0070) |
| QYL0076 | QYL0302 | AddServiceDefaults called without AddOpenTelemetry                                          | Configuration   | yes (`QYL0076MissingOTelConfigurationAnalyzer.cs`)               | yes (AL0076) |
| QYL0093 | QYL0303 | Missing essential resource attributes                                                       | Configuration   | yes (`QYL0093MissingResourceAttributesAnalyzer.cs`)              | yes (AL0093) |
| QYL0096 | QYL0304 | EventSourceSupport not enabled for AOT telemetry                                            | Configuration   | yes (`QYL0096EnableEventSourceSupportAnalyzer.cs`)               | yes (AL0096) |
| QYL0135 | QYL0305 | Legacy aggregated semantic-convention accessor types                                        | Configuration   | yes (`QYL0135LegacySemanticConventionsAccessorAnalyzer.cs`)      | yes (AL0135) |
| QYL0001 | QYL0400 | `gen_ai.execute_tool` span requires `gen_ai.tool.name`                                      | GenAI           | yes (`GenAiExecuteToolNameAnalyzer.cs`)                          | no (AL0001 only) |
| QYL0064 | QYL0401 | GenAI span missing required attributes                                                      | GenAI           | yes (`QYL0064GenAiMissingRequiredAttributesAnalyzer.cs`)         | yes (AL0064) |
| QYL0065 | QYL0402 | Use `gen_ai.client.token.usage` histogram                                                   | GenAI           | yes (`QYL0065UseTokenUsageHistogramAnalyzer.cs`)                 | yes (AL0065) |
| QYL0066 | QYL0403 | Invalid GenAI operation name                                                                | GenAI           | yes (`QYL0066InvalidGenAiOperationNameAnalyzer.cs`)              | yes (AL0066) |
| QYL0074 | QYL0404 | Deprecated GenAI semantic-convention attribute                                              | GenAI           | yes (`QYL0074DeprecatedGenAiAttributeAnalyzer.cs`)               | yes (AL0074) |
| QYL0124 | QYL0405 | `[AgentTraced]` on abstract/extern/partial method (non-interceptable)                       | GenAI           | yes (`QYL0124NonInterceptableAgentTracedAnalyzer.cs`)            | yes (AL0124) |
| QYL0131 | QYL0406 | Direct GenAI SDK usage (bypasses instrumentation)                                           | GenAI           | yes (`QYL0131DirectGenAiSdkUsageAnalyzer.cs`)                    | yes (AL0131) |
| QYL0071 | QYL0500 | `[Meter]` class must be partial static                                                      | Source gen      | yes (`QYL0071MeterClassMustBePartialStaticAnalyzer.cs`)          | yes (AL0071) |
| QYL0072 | QYL0501 | `[Counter]/[Histogram]` method must be partial                                              | Source gen      | yes (`QYL0072MetricMethodMustBePartialAnalyzer.cs`)              | yes (AL0072) |
| QYL0083 | QYL0600 | Insecure HTTP endpoint where HTTPS expected                                                 | Security        | yes (`QYL0083InsecureEndpointAnalyzer.cs`)                       | yes (AL0083) |
| QYL0088 | QYL0601 | Sensitive data (PII/credential) in span attribute                                           | Security        | yes (`QYL0088SensitiveDataInAttributeAnalyzer.cs`)               | yes (AL0088) |
| QYL0089 | QYL0700 | OTLP exporter without explicit endpoint                                                     | Export / perf   | yes (`QYL0089MissingOtlpConfigurationAnalyzer.cs`)               | yes (AL0089) |
| QYL0090 | QYL0701 | OTLP HTTP exporter without compression                                                      | Export / perf   | yes (`QYL0090UncompressedExportAnalyzer.cs`)                     | yes (AL0090) |
| QYL0091 | QYL0702 | SimpleSpanProcessor/SimpleActivityExportProcessor used                                      | Export / perf   | yes (`QYL0091BatchExportDisabledAnalyzer.cs`)                    | yes (AL0091) |
| QYL0092 | QYL0703 | OpenTelemetry tracing without sampling configured                                           | Export / perf   | yes (`QYL0092ConsiderSamplingAnalyzer.cs`)                       | yes (AL0092) |

---

## 3. Dead-ID list (delete on renumber)

105 IDs in `AnalyzerReleases.Shipped.md` are marked `Disabled` with the note:

> Reserved ID for catalog-derived diagnostic; runtime reports via QYL0030/QYL0031/QYL0032.

All 105 are emitted only as stub descriptors by `ReservedCatalogStubsAnalyzer.cs`. They were pre-allocated for a per-entry catalog feed that the supplemental migration analyzer never actually uses — at runtime, catalog hits always surface under QYL0030/QYL0031/QYL0032 (new QYL0009/0010/0011). They have no analyzer logic, no resx keys, and no consumer-visible behavior.

**Recommendation for 3.0.0 break:** delete all 105 stubs, delete `ReservedCatalogStubsAnalyzer.cs`, and stop pre-publishing reservations in `AnalyzerReleases.Shipped.md`. The supplemental migration analyzer already routes through three real IDs (new QYL0009/0010/0011); pre-reserving 105 placeholder IDs adds noise without enabling anything.

Reserved-stub IDs to delete:

```text
QYL0003 QYL0004 QYL0006 QYL0007 QYL0008 QYL0009 QYL0015 QYL0016 QYL0017
QYL0018 QYL0019 QYL0020 QYL0022 QYL0023 QYL0024 QYL0025 QYL0026 QYL0027
QYL0028 QYL0029 QYL0033 QYL0034 QYL0035 QYL0036 QYL0037 QYL0038 QYL0039
QYL0040 QYL0041 QYL0042 QYL0043 QYL0044 QYL0045 QYL0046 QYL0047 QYL0048
QYL0049 QYL0050 QYL0051 QYL0052 QYL0053 QYL0054 QYL0055 QYL0056 QYL0057
QYL0058 QYL0059 QYL0060 QYL0062 QYL0080 QYL0081 QYL0082 QYL0084 QYL0087
QYL0094 QYL0095 QYL0097 QYL0098 QYL0099 QYL0100 QYL0101 QYL0102 QYL0103
QYL0104 QYL0105 QYL0106 QYL0111 QYL0112 QYL0114 QYL0115 QYL0116 QYL0117
QYL0118 QYL0119 QYL0120 QYL0121 QYL0122 QYL0123 QYL0125 QYL0126 QYL0127
QYL0132 QYL0133 QYL0134 QYL0136 QYL0137 QYL0138 QYL0139 QYL0140 QYL0141
QYL0142 QYL0143 QYL0144 QYL0145 QYL0146 QYL0147 QYL0148 QYL0149 QYL0150
QYL0151 QYL0152 QYL0153 QYL0154 QYL0155 QYL0156
```

No other dead IDs found: every ID in `Shipped.md` is declared somewhere in source, and every QYL literal in source appears in `Shipped.md`. There are no declared-but-unshipped IDs and no shipped-but-undeclared IDs.

---

## 4. Resource-key rename rule

The repo's analyzer base class (`AlAnalyzer.CreateRule`, `AlAnalyzer.cs:36-42`) computes resource keys from the *DiagnosticId literal*:

```csharp
new LocalizableResourceString($"{id}AnalyzerTitle",        Resources.ResourceManager, ...)
new LocalizableResourceString($"{id}AnalyzerMessageFormat", Resources.ResourceManager, ...)
new LocalizableResourceString($"{id}AnalyzerDescription",  Resources.ResourceManager, ...)
```

But every resx key in the repo today uses the **`AL####`** prefix, not `QYL####`. The `Resources.resx` and `CodeFixResources.resx` files contain **zero** keys starting with `QYL`. So all current resource lookups for QYL analyzers are falling back to the key name itself — the `[Resources.AL0061AnalyzerTitle]` etc. entries are dead resources, and the diagnostics show raw keys to consumers.

This is a pre-existing bug. The 3.0 renumber is the right moment to fix it.

### Rule (apply to both `Resources.resx` and `CodeFixResources.resx`)

For each old `AL####` key whose numeric segment matches an old QYL ID being renumbered (e.g., the analyzer file's `DiagnosticId = "QYL0061"` corresponds to `AL0061AnalyzerTitle`):

1. Rename `AL{oldNum}` → `QYL{newNum}` so the prefix matches the live `DiagnosticId` literal that `CreateRule` interpolates.
2. Apply to every suffix variant: `AnalyzerTitle`, `AnalyzerMessageFormat`, `AnalyzerDescription`, `CodeFixTitle`, and any indexed variants (e.g., `AL0030ImplementsCodeFixTitle` → `QYL0009ImplementsCodeFixTitle`, `AL0031CodeFixTitleTryGetConstantValue` → `QYL0010CodeFixTitleTryGetConstantValue`).
3. Delete `AL####` entries whose numeric ID does **not** correspond to any active QYL rule (these are leftovers from a previous incarnation — see "Orphan AL keys" below).

### Per-suffix rename pattern

```
AL{old}AnalyzerTitle           -> QYL{new}AnalyzerTitle
AL{old}AnalyzerMessageFormat   -> QYL{new}AnalyzerMessageFormat
AL{old}AnalyzerDescription     -> QYL{new}AnalyzerDescription
AL{old}CodeFixTitle            -> QYL{new}CodeFixTitle
AL{old}{Variant}CodeFixTitle   -> QYL{new}{Variant}CodeFixTitle
```

### Concrete resx keys to rename

Driven by old-to-new mapping in section 2. Only the QYL IDs that own an `AL####` resx key today (right-most column of the mapping table) generate renames:

| Old resx key prefix | New resx key prefix |
|---------------------|---------------------|
| AL0013              | QYL0006             |
| AL0061              | QYL0100             |
| AL0063              | QYL0101             |
| AL0064              | QYL0401             |
| AL0065              | QYL0402             |
| AL0066              | QYL0403             |
| AL0067              | QYL0200             |
| AL0068              | QYL0201             |
| AL0069              | QYL0300             |
| AL0070              | QYL0301             |
| AL0071              | QYL0500             |
| AL0072              | QYL0501             |
| AL0073              | QYL0102             |
| AL0074              | QYL0404             |
| AL0075              | QYL0202             |
| AL0076              | QYL0302             |
| AL0077              | QYL0103             |
| AL0078              | QYL0104             |
| AL0079              | QYL0105             |
| AL0083              | QYL0600             |
| AL0085              | QYL0012             |
| AL0086              | QYL0013             |
| AL0088              | QYL0601             |
| AL0089              | QYL0700             |
| AL0090              | QYL0701             |
| AL0091              | QYL0702             |
| AL0092              | QYL0703             |
| AL0093              | QYL0303             |
| AL0096              | QYL0304             |
| AL0107              | QYL0106             |
| AL0108              | QYL0107             |
| AL0109              | QYL0108             |
| AL0110              | QYL0109             |
| AL0113              | QYL0110             |
| AL0124              | QYL0405             |
| AL0131              | QYL0406             |
| AL0135              | QYL0305             |

37 prefix groups (matches the 37 QYL analyzer files that route resource lookups through `CreateRule`). The 11 active rules declared inside `DiagnosticDescriptors.cs` (QYL0001, 0002, 0005, 0010, 0011, 0012, 0014, 0021, 0030, 0031, 0032) hard-code their title/message/description strings inline; they have no resx keys and produce **zero** resx renames.

### Orphan AL keys (delete on renumber)

Resx contains hundreds of `AL####` keys that have no matching QYL diagnostic and never did in this repo — they appear to be leftovers from an upstream `ANcpLua.*.Analyzers` codebase that this analyzer assembly was forked from. Suggested cleanup as part of the same PR:

- Delete all `AL####AnalyzerTitle/MessageFormat/Description` keys whose numeric ID is **not** in the rename table above (e.g., `AL0001`, `AL0002`, ..., `AL0011`, `AL0017`-`AL0060` excluding the seven listed above, `AL0103`, `AL0121`, `AL0122`, `AL0126`, `AL0134`, `AL0137`, `AL0138`, etc.).
- Delete the corresponding entries from `Resources.Designer.cs` (auto-regenerated by `dotnet msbuild /t:CoreResGen` after resx edits — do not hand-edit).

This is independent of the QYL renumber but folding it into the 3.0 break costs nothing and removes ~400 dead keys.

---

## 5. Sanity checks

- **Old distinct active IDs covered:** 48  (11 in `DiagnosticDescriptors.cs` + 37 in `QYL####*.cs` files)
- **Old distinct reserved-disabled IDs deleted:** 105  (all of `ReservedCatalogStubsAnalyzer.cs`)
- **Total old IDs in `AnalyzerReleases.Shipped.md`:** 153  (48 + 105)
- **New distinct IDs assigned:** 48  (matches active count, equals 153 minus 105 deletions)
- **New ID range used:** `QYL0001..QYL0013`, `QYL0100..QYL0110`, `QYL0200..QYL0202`, `QYL0300..QYL0305`, `QYL0400..QYL0406`, `QYL0500..QYL0501`, `QYL0600..QYL0601`, `QYL0700..QYL0703`  (8 contiguous segments across 8 bands, max density 13/100 in the Semantic-conventions band)
- **Resx prefix groups renamed:** 37  (one per QYL analyzer file that calls `CreateRule`; the 11 inline-string rules in `DiagnosticDescriptors.cs` do not consume resources)

### Files to touch (mechanical)

1. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/DiagnosticDescriptors.cs` — rewrite each of the 11 `id: "QYL####"` literals and each `HelpLinkBase + "qyl####"` anchor.
2. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/QYL####*.cs` (37 files) — rewrite `DiagnosticId = "QYL####"`. **Renaming the source files themselves** (e.g., `QYL0013MissingSchemaUrlAnalyzer.cs` -> `QYL0006MissingSchemaUrlAnalyzer.cs`) is recommended for searchability; the class names start with `Al####` so they are unaffected (and could optionally be renamed in a follow-up).
3. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/AnalyzerReleases.Shipped.md` — clear file, replace `## Release 2.0.0` with `## Release 3.0.0`, write 48 new entries in `QYL####` ascending order.
4. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/ReservedCatalogStubsAnalyzer.cs` — **delete the file**.
5. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/Resources.resx` and `CodeFixResources.resx` — apply the 37 prefix renames in section 4, then regenerate the Designer files.
6. `src/Qyl.OpenTelemetry.SemanticConventions.Analyzers/SemconvLegacyContextSuppressor.cs` line 196 — the suppression-ID derivation `"QYL9" + id.Substring(4)` keeps working unchanged; it derives a `QYL9XXXX` suppressor ID from each new diagnostic ID at runtime. No edit needed.
7. Tests under `tests/` that reference old IDs as string literals will be picked up by a repo-wide `QYL\d{4}` regex replacement using the mapping table above.

### Notes / non-goals

- `QYL9####` suppressor IDs are derived at runtime in `SemconvLegacyContextSuppressor.cs` and are not subject to renumbering.
- The `AL####` prefix in resx keys and analyzer class names (`Al0013MissingSchemaUrlAnalyzer`) is a fork-time artifact from an upstream `ANcpLua` analyzer assembly. Renaming `Al0013…` class names to `Qyl0006…` is optional; the public diagnostic ID is the contract that matters for consumers.
- The `DiagnosticCategories.OpenTelemetry / GenAI / Metrics / Configuration` taxonomy used in `CreateRule(...)` is independent of these ID bands and need not change; the band scheme is for ID assignment only.
