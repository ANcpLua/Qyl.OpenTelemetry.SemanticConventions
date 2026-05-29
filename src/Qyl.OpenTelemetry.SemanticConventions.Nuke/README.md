# Qyl.OpenTelemetry.SemanticConventions.Nuke

[![NuGet Qyl.OpenTelemetry.SemanticConventions](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions?label=Qyl.OpenTelemetry.SemanticConventions&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Incubating](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Incubating?label=.Incubating&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.SourceGeneration](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration?label=.SourceGeneration&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Analyzers](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Analyzers?label=.Analyzers&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/)
[![NuGet Qyl.OpenTelemetry.SemanticConventions.Nuke](https://img.shields.io/nuget/v/Qyl.OpenTelemetry.SemanticConventions.Nuke?label=.Nuke&color=0891B2)](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Nuke/)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://www.apache.org/licenses/LICENSE-2.0)

Nuke build component for the Qyl OpenTelemetry semantic-conventions toolchain.

Compatible with: Nuke.Common 10.x
Targets: `net10.0`

Exposes `IUpstreamConventions` (Weaver-based generator pipeline with lockstep verification) and `IDomainConventionsApi` (downstream TypeSpec API pipeline) component interfaces, plus the `LockstepPolicy` helper for `{semconv}-{n}` version-suffix parsing.

| Channel | Package | Contents |
|---|---|---|
| stable | [`Qyl.OpenTelemetry.SemanticConventions`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions/) | Stable attribute-key constants + embedded resolved schema |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Incubating`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Incubating/) | Incubating attribute-key constants (opt-in, breaking between minors) |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.SourceGeneration`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.SourceGeneration/) | Roslyn source generators for attribute constants, Activity tag setters, metric descriptors, Meter factories, and event payloads |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Analyzers`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Analyzers/) | Roslyn diagnostic analyzers + code fixes |
| stable | [`Qyl.OpenTelemetry.SemanticConventions.Nuke`](https://www.nuget.org/packages/Qyl.OpenTelemetry.SemanticConventions.Nuke/) | Nuke build component exposing the Weaver and TypeSpec pipelines |

Siblings: [ANcpLua.Agents](https://github.com/ANcpLua/ANcpLua.Agents) · [ANcpLua.Roslyn.Utilities](https://github.com/ANcpLua/ANcpLua.Roslyn.Utilities) · [ANcpLua.NET.Sdk](https://github.com/ANcpLua/ANcpLua.NET.Sdk) · [ANcpLua.Analyzers](https://github.com/ANcpLua/ANcpLua.Analyzers)
