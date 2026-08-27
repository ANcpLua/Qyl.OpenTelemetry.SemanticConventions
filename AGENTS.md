# Qyl telemetry semantic-conventions contract

Owns pinned OpenTelemetry registry inputs and deterministic generation of stable,
incubating, analyzer, source-generation, registry-resource, and TypeSpec-key
artifacts. It owns vocabulary, not instrumentation, product DTOs, or storage.

Edit pins, qyl registry inputs, templates, or generators; never edit generated
constants, registries, snapshots, or analyzer pages directly. Do not mint
unratified stable `mcp.*` vocabulary; experimental qyl concepts stay staged and
are reconsidered at every upstream pin update.

The compiled packages are consumers of the repository's own source generator: their
constant classes are projected from `resolved-registry.json` at build time, so there
is no checked-in constant tree. Validate the Release build, both test executables,
and every generator `--check` command documented by the owning script. Regenerate
with the corresponding `--write` command and inspect diffs. Publishing is CI
OIDC only.
