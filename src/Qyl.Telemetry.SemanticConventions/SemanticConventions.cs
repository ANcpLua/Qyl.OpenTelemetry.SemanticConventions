using Qyl.Telemetry.SemanticConventions.SourceGeneration;

// The stable tier of the pinned registry, projected by the repository's own source
// generator at build time: one Attributes.{Root}.{Root}Attributes class per registry
// root with a stable or deprecated row, plus SchemaUrl and the qyl-owned scope and
// event names as Names.QylTelemetryNames. The scope names are what the Qyl.Telemetry
// producer packages construct their ActivitySource and Meter with, and qyl's
// architecture forbids those packages from reading the incubating tier, so the names
// live here rather than in .Incubating. The definition types under Definitions/ are
// hand-written; everything else this package ships is generated from
// Qyl.Telemetry.SemanticConventions.SourceGeneration/Resources/resolved-registry.json.
[assembly: SemanticConventionAttributesPackage("Qyl.Telemetry.SemanticConventions")]
[assembly: SemanticConventionTelemetryNamesPackage("Qyl.Telemetry.SemanticConventions")]
