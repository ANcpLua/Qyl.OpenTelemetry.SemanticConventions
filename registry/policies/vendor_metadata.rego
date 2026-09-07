package after_resolution

import rego.v1

# A vendor group declares the keys a pinned third-party library puts on its own ActivitySource
# and that upstream semantic conventions do not define. Every such group needs its metadata
# sibling: the library, its exact pinned version, the repository and ref the finding was read
# at, the license, and the ActivitySources it emits on. This is the check merge_registries.py
# used to carry on the `vendor_models` entries.

vendor_ids contains id if {
	some group in input.groups
	startswith(group.id, "registry.vendor.")
	id := substring(group.id, count("registry.vendor."), -1)
}

metadata_ids contains id if {
	some group in input.groups
	startswith(group.id, "vendor.")
	id := substring(group.id, count("vendor."), -1)
}

deny contains vendor_violation(description, group_id, "-") if {
	some id in vendor_ids
	not id in metadata_ids
	group_id := sprintf("registry.vendor.%s", [id])
	description := sprintf(
		"Vendor group 'registry.vendor.%s' has no 'vendor.%s' metadata group. Every vendor group needs the library, version, repository, ref, license and activity_sources next to it.",
		[id, id],
	)
}

required_vendor_fields := {"library", "version", "repository", "ref", "license", "activity_sources"}

deny contains vendor_violation(description, group.id, field) if {
	some group in input.groups
	startswith(group.id, "vendor.")
	some field in required_vendor_fields
	not group.annotations.qyl.vendor[field]
	description := sprintf("Vendor metadata group '%s' is missing annotations.qyl.vendor.%s.", [group.id, field])
}

deny contains vendor_violation(description, group.id, "activity_sources") if {
	some group in input.groups
	startswith(group.id, "vendor.")
	count(group.annotations.qyl.vendor.activity_sources) == 0
	description := sprintf("Vendor metadata group '%s' declares no activity_sources.", [group.id])
}

deny contains vendor_violation(description, group.id, source.name) if {
	some group in input.groups
	startswith(group.id, "vendor.")
	some source in group.annotations.qyl.vendor.activity_sources
	not source.note
	description := sprintf(
		"ActivitySource '%s' in '%s' has no note. The note is the file and line the name was read at in the pinned release.",
		[source.name, group.id],
	)
}

# Every vendor attribute cites where the library sets it.
deny contains vendor_violation(description, group.id, attr.name) if {
	some group in input.groups
	startswith(group.id, "registry.vendor.")
	some attr in group.attributes
	not attr.note
	description := sprintf(
		"Vendor attribute '%s' in '%s' has no note. The note is the file and line of the upstream finding.",
		[attr.name, group.id],
	)
}

vendor_violation(description, group, attr) := {
	"id": description,
	"type": "semconv_attribute",
	"category": "vendor_metadata",
	"group": group,
	"attr": attr,
}
