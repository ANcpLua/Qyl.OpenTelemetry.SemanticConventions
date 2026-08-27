using Qyl.Telemetry.SemanticConventions.SourceGeneration;

// Every tier of the pinned registry, projected by the repository's own source generator
// at build time: one Attributes.{Root}.{Root}Attributes class per registry root, plus the
// qyl-owned scope and event names as Names.QylTelemetryNames. Registry/ and schemas/ are
// emitted by emit_registry_resources.py from the same resolved-registry.json.
[assembly: SemanticConventionIncubatingAttributesPackage("Qyl.Telemetry.SemanticConventions.Incubating")]
[assembly: SemanticConventionTelemetryNamesPackage("Qyl.Telemetry.SemanticConventions.Incubating")]
