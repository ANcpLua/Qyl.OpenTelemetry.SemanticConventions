# Where should `dotnet_wcf` live now that `rpc.system` is deprecated?

Draft issue for open-telemetry/semantic-conventions. Not filed.

## What I checked

Pinned core registry `v1.44.0` (`Version.props` → `SemConvSchemaVersion`), as projected
into `src/Qyl.Telemetry.SemanticConventions.SourceGeneration/Resources/resolved-registry.json`
(`schema_url: https://opentelemetry.io/schemas/1.44.0`, `semconv_commit e10a930844c6951757a43b849d364f7d056ac32b`),
and the corresponding source YAML in the vendored checkout:

- `model/rpc/deprecated/registry-deprecated.yaml` — `rpc.system` carries
  `deprecated: {reason: renamed, renamed_to: rpc.system.name}`, brief
  "Deprecated, use `rpc.system.name` attribute instead.", and still lists the member
  `dotnet_wcf` (`value: 'dotnet_wcf'`, `brief: '.NET WCF'`, `stability: development`).
- `model/rpc/registry.yaml` — the replacement `rpc.system.name`
  (`stability: release_candidate`) declares exactly four members: `grpc`, `dubbo`,
  `connectrpc`, `jsonrpc`.

## The problem

`dotnet_wcf` exists only as a member of a renamed-away attribute. Following the
`renamed_to` pointer to `rpc.system.name` lands on an enum that has no WCF value, so an
instrumentation emitting WCF spans today has no non-deprecated spelling to migrate to.
Automated migration tooling that rewrites `rpc.system` → `rpc.system.name` produces a
value the registry does not know.

## Questions

1. Was the omission of a WCF member from `rpc.system.name` deliberate (WCF considered
   out of scope for the RC surface), or an oversight in the rename?
2. If deliberate, what is the intended attribute for WCF spans — a different namespace,
   or no registry value at all?

## Proposal

Add a WCF member to `rpc.system.name` — suggested `id: dotnet_wcf`, `value: 'wcf'`
(the other RC members dropped their language/vendor prefixes: `apache_dubbo` → `dubbo`,
`connect_rpc` → `connectrpc`), `stability: development` — and record the value mapping
on the deprecated `rpc.system` member so migration tooling can follow it. If a WCF value
is intentionally excluded, state that in the `rpc.system` deprecation note so downstreams
stop looking for a target.
