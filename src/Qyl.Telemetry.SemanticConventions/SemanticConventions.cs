using Qyl.Telemetry.SemanticConventions.SourceGeneration;

// The stable tier of the pinned registry, projected by the repository's own source
// generator at build time: one Attributes.{Root}.{Root}Attributes class per registry
// root with a stable or deprecated row, plus SchemaUrl. The definition types under
// Definitions/ are hand-written; everything else this package ships is generated
// from Qyl.Telemetry.SemanticConventions.SourceGeneration/Resources/resolved-registry.json.
[assembly: SemanticConventionAttributesPackage("Qyl.Telemetry.SemanticConventions")]
