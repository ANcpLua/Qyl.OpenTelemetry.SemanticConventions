package after_resolution

import rego.v1

# Checked against the materialized v2 registry (`--v2 --include-unreferenced`), which is the
# only shape that carries the resolved catalog of every dependency. The plain
# `registry/policies` run sees only qyl's own groups.
#
# `qyl.attribute.namespace` is the closed value set the collector's dropped-attribute counter
# is broken down by: the first segment of every attribute in the resolved catalog, plus
# `other` for a key the registry does not know. A registry bump that adds a root namespace
# must add its member here, or the counter silently buckets a whole namespace as `other`.

catalog_roots contains root if {
	some attr in input.registry.attributes
	root := split(attr.key, ".")[0]
}

catalog_roots contains root if {
	some dependency in input.dependencies
	some attr in dependency.registry.attributes
	root := split(attr.key, ".")[0]
}

namespace_members contains value if {
	some attr in input.registry.attributes
	attr.key == "qyl.attribute.namespace"
	some member in attr.type.members
	value := member.value
}

deny contains namespace_violation(description) if {
	some root in catalog_roots
	not root in namespace_members
	description := sprintf(
		"Root namespace '%s' is in the resolved catalog but is not a member of qyl.attribute.namespace. Add it to registry/qyl/attributes.yaml.",
		[root],
	)
}

deny contains namespace_violation(description) if {
	some value in namespace_members
	value != "other"
	not value in catalog_roots
	description := sprintf(
		"qyl.attribute.namespace declares '%s', which is no longer a root namespace in the resolved catalog. Delete the member.",
		[value],
	)
}

deny contains namespace_violation("qyl.attribute.namespace has no `other` member; a key outside the registry has no bucket.") if {
	count(namespace_members) > 0
	not "other" in namespace_members
}

namespace_violation(description) := {
	"id": description,
	"type": "semconv_attribute",
	"category": "qyl_attribute_namespace",
	"group": "registry.qyl",
	"attr": "qyl.attribute.namespace",
}
