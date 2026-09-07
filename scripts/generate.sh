#!/usr/bin/env bash
# Regenerates every committed generated file from registry/ with Weaver. There is no
# intermediate JSON and no compile-time generator: Weaver's materialized v2 registry goes
# straight into the Jinja templates under templates/, and the output is committed.
# scripts/check-generated.sh runs this and fails on `git diff`.
#
# Pins live in exactly one place, registry/manifest.yaml. Version.props keeps WeaverVersion
# so CI's setup-weaver and this guard agree on the binary.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/.." && pwd)"
cd "${repo_root}"

WEAVER="${WEAVER:-weaver}"
read -r -a WEAVER_CMD <<< "${WEAVER}"

expected_weaver_version="$(python3 - Version.props <<'PY'
import sys
import xml.etree.ElementTree as ET

value = ET.parse(sys.argv[1]).getroot().findtext(".//WeaverVersion")
if not value or not value.strip():
    raise SystemExit("error: Version.props does not define WeaverVersion")
print(value.strip())
PY
)"

actual_weaver_version="$("${WEAVER_CMD[@]}" --version | awk '{print $2}')"
if [[ "${actual_weaver_version}" != "${expected_weaver_version}" ]]; then
  echo "error: Weaver ${expected_weaver_version} is required; found ${actual_weaver_version}" >&2
  echo "set WEAVER to the pinned binary or intentionally update WeaverVersion in Version.props" >&2
  exit 1
fi

"${script_dir}/fetch-core.sh"

core_ref="$(cat .build/core-ref.txt)"
core_commit="$(cat .build/core-commit.txt)"

# The three schema URLs are registry facts: Weaver's materialized v2 registry carries them and
# the template filters read them off it. The git ref the genai dependency is pinned to is not
# part of that registry, so it is read out of the manifest here and handed over as a parameter.
genai_commit="$(python3 - registry/manifest.yaml <<'PY'
import sys
import yaml

manifest = yaml.safe_load(open(sys.argv[1])) or {}
by_name = {d["name"]: d for d in manifest.get("dependencies") or []}
print(by_name["genai"]["registry_path"].split("@", 1)[1].split("[", 1)[0])
PY
)"

# Weaver's materialized v2 registry flattens `registry.*` groups into the catalog and drops
# their group annotations, so the two lists the templates need — the scope names qyl
# constructs and the ActivitySource names of the pinned vendor libraries — are read out of the
# same YAML here and handed to the templates as parameters. registry/ stays the only source.
scope_names="$(python3 - registry/qyl/names.yaml <<'PY'
import sys
import yaml

for group in (yaml.safe_load(open(sys.argv[1])) or {}).get("groups") or []:
    names = ((group.get("annotations") or {}).get("qyl") or {}).get("scope_names")
    if names:
        print(",".join(sorted(set(names))))
        break
else:
    raise SystemExit("error: registry/qyl/names.yaml declares no annotations.qyl.scope_names")
PY
)"

vendor_activity_sources="$(python3 - <<'PY'
import glob
import yaml

names = set()
for path in sorted(glob.glob("registry/vendor/*.yaml")):
    for group in (yaml.safe_load(open(path)) or {}).get("groups") or []:
        vendor = ((group.get("annotations") or {}).get("qyl") or {}).get("vendor")
        if not vendor:
            continue
        for source in vendor.get("activity_sources") or []:
            names.add(source["name"])
if not names:
    raise SystemExit("error: registry/vendor/*.yaml declare no activity_sources")
print(",".join(sorted(names)))
PY
)"

"${WEAVER_CMD[@]}" registry check -r registry
"${WEAVER_CMD[@]}" registry check -r registry --v2 --include-unreferenced -p registry/policies-v2

"${WEAVER_CMD[@]}" registry generate \
  -r registry \
  --v2 \
  --include-unreferenced \
  -t templates \
  -D "weaver_version=${actual_weaver_version}" \
  -D "core_ref=${core_ref}" \
  -D "core_commit=${core_commit}" \
  -D "genai_commit=${genai_commit}" \
  -D "scope_names=${scope_names}" \
  -D "vendor_activity_sources=${vendor_activity_sources}" \
  csharp \
  .

echo "Regenerated from registry/ with Weaver ${actual_weaver_version}"
echo "  core:  ${core_ref} (${core_commit})"
echo "  genai: ${genai_commit}"
