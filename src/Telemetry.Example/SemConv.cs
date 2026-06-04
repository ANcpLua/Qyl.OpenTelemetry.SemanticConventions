using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

namespace Telemetry.Example;

// The generator fills these partials from the pinned semantic-conventions v1.41.0
// registry. gen_ai is incubating, so use the Incubating markers (stable + dev/alpha
// + deprecated). A named namespace is required: a global-namespace partial collides
// with Program.cs's top-level statements (CS9348).

[SemanticConventionIncubatingAttributes("gen_ai")]
internal static partial class GenAi;

[SemanticConventionIncubatingMeters("gen_ai.client")]
internal static partial class GenAiMeters;
