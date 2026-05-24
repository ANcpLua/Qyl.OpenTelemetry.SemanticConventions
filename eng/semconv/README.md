# eng/semconv

Build-time inputs and lock state for the semantic-conventions pipeline.

| File | Role |
|---|---|
| `attributes.lock.sha256` | Manifest hash of every committed `src/*/Attributes/**/*.g.cs`. Written by `./build.sh SeedAttributesHash`, verified by `./build.sh VerifyAttributesHash`. Drift fails CI. |

The Weaver registry input itself lives at the repo root (`master-programmatic.yaml`) because it is the human-reviewable upstream pin; this directory only contains derived lock state.
