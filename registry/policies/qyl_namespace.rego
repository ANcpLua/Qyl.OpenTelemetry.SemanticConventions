package after_resolution

import rego.v1

# Every attribute qyl declares itself lives under `qyl.`. A key outside that namespace reaches
# the registry only through a vendor group, which names the library, its exact pinned version,
# the repository and ref the finding was read at, and the ActivitySources it emits on.
deny contains attr_registry_violation(description, group.id, attr.name) if {
	some group in input.groups
	group.id == "registry.qyl"
	some attr in group.attributes
	not startswith(attr.name, "qyl.")
	description := sprintf(
		"Attribute '%s' is declared in the qyl group but is not in the qyl.* namespace. A key outside qyl.* belongs in a vendor group under registry/vendor/.",
		[attr.name],
	)
}

attr_registry_violation(description, group, attr) := {
	"id": description,
	"type": "semconv_attribute",
	"category": "qyl_namespace",
	"group": group,
	"attr": attr,
}
