# Qyl.OpenTelemetry.SemanticConventions.Analyzers

[![NuGet Qyl.OpenTelemetry.SemanticConventions](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions?label=Qyl.OpenTelemetry.SemanticConventions&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Incubating](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Incubating?label=.Incubating&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.SourceGeneration](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration?label=.SourceGeneration&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Analyzers](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Analyzers?label=.Analyzers&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Nuke](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Nuke?label=.Nuke&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Nuke/)
[![.NET](https://img.shields.io/badge/.NET-netstandard2.0-512BD4)](https://dotnet.microsoft.com/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://www.apache.org/licenses/LICENSE-2.0)

Roslyn diagnostic analyzers and code fixes for OpenTelemetry consumers — flags deprecated semantic-convention usage, schema-URL omission, missing GenAI required attributes, ActivitySource and Meter registration gaps, sensitive-data leakage, and OTLP export misconfiguration across 48 rules in 8 domain bands.

Targets: `netstandard2.0` (Roslyn host requirement)

| Channel | Package | Contents |
|---|---|---|
| stable | [`Qyl.OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions/) | Stable attribute-key constants + embedded resolved schema |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Incubating`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/) | Incubating attribute-key constants (opt-in, breaking between minors) |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.SourceGeneration`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/) | Roslyn source generators for Activity tags, Meter factories, metrics, events |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Analyzers`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/) | Roslyn diagnostic analyzers + code fixes |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Nuke`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Nuke/) | Nuke build component exposing the Weaver and TypeSpec pipelines |

## Domain bands

| Range | Domain | Rules |
|---|---|---|
| `QYL0001..0099` | Semantic conventions (catalog migration, deprecated members/values, opt-in, library hygiene, attribute type/value validation, schema URL) | 13 |
| `QYL0100..0199` | Tracing (Activity, ActivitySource, `[Traced]`, `[TracedTag]`, `[NoTrace]`, span shape, exception recording) | 11 |
| `QYL0200..0299` | Metrics (Meter registration, instrument naming, tag cardinality) | 3 |
| `QYL0300..0399` | Configuration / setup (ServiceDefaults, collector endpoint, OTel composition, resource attributes, EventSourceSupport, legacy accessor types) | 6 |
| `QYL0400..0499` | GenAI (`gen_ai.*` semconv, agent tracing, direct SDK usage, token-usage histogram, operation names) | 7 |
| `QYL0500..0599` | Source generator / partial enforcement (`[Meter]`, `[Counter]`/`[Histogram]` partial requirements) | 2 |
| `QYL0600..0699` | Security / sensitive data (insecure endpoint, PII/credential leakage) | 2 |
| `QYL0700..0799` | Export / batching / performance (OTLP endpoint, compression, SimpleSpanProcessor, sampling) | 4 |

## Rules

### Semantic conventions (`QYL0001..0013`)

| ID | Severity | Title |
|---|---|---|
| QYL0001 | Info | graphql.document is opt-in |
| QYL0002 | Warning | RPC server span must not include client.address / client.port |
| QYL0003 | Warning | Deprecated semantic-convention constant |
| QYL0004 | Warning | Prefer typed semantic-convention constant over string literal |
| QYL0005 | Warning | String literal matches a deprecated semantic-convention name |
| QYL0006 | Warning | Missing telemetry schema URL |
| QYL0007 | Warning | Deprecated semantic-convention value |
| QYL0008 | Warning | Incubating semantic-convention member used in a library |
| QYL0009 | Error | Obsolete semantic convention has an exact replacement |
| QYL0010 | Warning | Semantic convention migration needs review |
| QYL0011 | Warning | Legacy semantic convention appears in compatibility or test code |
| QYL0012 | Warning | Invalid attribute value (violates OTel semconv specification) |
| QYL0013 | Warning | Incorrect attribute type |

### Tracing (`QYL0100..0110`)

| ID | Severity | Title |
|---|---|---|
| QYL0100 | Warning | Activity/Span missing semantic convention attributes |
| QYL0101 | Warning | Unregistered ActivitySource (missing AddSource) |
| QYL0102 | Warning | `[Traced]` requires non-empty ActivitySourceName |
| QYL0103 | Warning | Duplicate instrumentation (auto + manual span) |
| QYL0104 | Warning | Invalid ActivitySource name (reverse-DNS) |
| QYL0105 | Warning | Complex async pattern recommends manual span |
| QYL0106 | Warning | `[TracedTag]` on parameter where neither method nor type has `[Traced]` |
| QYL0107 | Warning | `[NoTrace]` on a method whose declaring type has no class-level `[Traced]` |
| QYL0108 | Warning | `[Traced]` on abstract/extern/partial method (non-interceptable) |
| QYL0109 | Warning | `[TracedTag]` on out/ref parameter |
| QYL0110 | Warning | `Activity.SetStatus(Error)` inside catch without RecordException |

### Metrics (`QYL0200..0202`)

| ID | Severity | Title |
|---|---|---|
| QYL0200 | Warning | Unregistered Meter (missing AddMeter) |
| QYL0201 | Warning | Invalid metric instrument name |
| QYL0202 | Warning | High-cardinality metric tag |

### Configuration (`QYL0300..0305`)

| ID | Severity | Title |
|---|---|---|
| QYL0300 | Warning | Incomplete ServiceDefaults configuration |
| QYL0301 | Warning | Non-OTLP collector endpoint |
| QYL0302 | Warning | AddServiceDefaults called without AddOpenTelemetry |
| QYL0303 | Warning | Missing essential resource attributes |
| QYL0304 | Warning | EventSourceSupport not enabled for AOT telemetry |
| QYL0305 | Warning | Legacy aggregated semantic-convention accessor types |

### GenAI (`QYL0400..0406`)

| ID | Severity | Title |
|---|---|---|
| QYL0400 | Warning | `gen_ai.execute_tool` span requires `gen_ai.tool.name` |
| QYL0401 | Warning | GenAI span missing required attributes |
| QYL0402 | Warning | Use `gen_ai.client.token.usage` histogram |
| QYL0403 | Warning | Invalid GenAI operation name |
| QYL0404 | Warning | Deprecated GenAI semantic-convention attribute |
| QYL0405 | Warning | `[AgentTraced]` on abstract/extern/partial method (non-interceptable) |
| QYL0406 | Warning | Direct GenAI SDK usage (bypasses instrumentation) |

### Source generator (`QYL0500..0501`)

| ID | Severity | Title |
|---|---|---|
| QYL0500 | Warning | `[Meter]` class must be partial static |
| QYL0501 | Warning | `[Counter]`/`[Histogram]` method must be partial |

### Security (`QYL0600..0601`)

| ID | Severity | Title |
|---|---|---|
| QYL0600 | Warning | Insecure HTTP endpoint where HTTPS expected |
| QYL0601 | Warning | Sensitive data (PII/credential) in span attribute |

### Export / performance (`QYL0700..0703`)

| ID | Severity | Title |
|---|---|---|
| QYL0700 | Warning | OTLP exporter without explicit endpoint |
| QYL0701 | Warning | OTLP HTTP exporter without compression |
| QYL0702 | Warning | SimpleSpanProcessor/SimpleActivityExportProcessor used |
| QYL0703 | Warning | OpenTelemetry tracing without sampling configured |

## Usage

```xml
<PackageReference Include="Qyl.OpenTelemetry.SemanticConventions.Analyzers"
                  Version="3.0.0"
                  PrivateAssets="all"
                  IncludeAssets="analyzers; buildtransitive" />
```

Siblings: [ANcpLua.Agents](https://github.com/ANcpLua/ANcpLua.Agents) · [ANcpLua.Roslyn.Utilities](https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities) · [ANcpLua.NET.Sdk](https://github.com/ANcpLua/ANcpLua.NET.Sdk) · [ANcpLua.Analyzers](https://github.com/ANcpLua/ANcpLua.Analyzers)
