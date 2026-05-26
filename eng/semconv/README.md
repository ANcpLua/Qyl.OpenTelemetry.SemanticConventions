# eng/semconv

Lock state for the semantic-conventions regeneration pipeline.

| File | Role |
|---|---|
| `attributes.lock.sha256` | Manifest hash of every committed `src/*/Attributes/**/*.g.cs`. Written by `./build.sh SeedAttributesHash`, verified by `./build.sh VerifyAttributesHash`. Drift fails CI. |

Regeneration itself happens out-of-band: a human runs Weaver against the OTel registry at the version pinned in `Version.props` (`SemConvSchemaVersion`), commits the new `.g.cs`, and refreshes this lock.
