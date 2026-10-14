#!/usr/bin/env bash
# Install and execute the real tarball or published npm version outside this repository.
set -euo pipefail
version=${1:?usage: verify-typescript.sh version [tarball]}
source=${2:-@ancplua/qyl-semantic-conventions@$version}
# VerifyRelease passes the fingerprint read from the release tag; otherwise the built tree's.
expected_digest=${QYL_EXPECTED_REGISTRY_SHA256:-$(node --input-type=module -e 'import {REGISTRY_SNAPSHOT_SHA256 as hash} from "./typescript/dist/index.js"; console.log(hash)')}
temp=$(mktemp -d)
trap 'rm -rf "$temp"' EXIT
npm install --prefix "$temp" --registry=https://registry.npmjs.org --ignore-scripts --no-audit --no-fund "$source" >/dev/null
cat > "$temp/verify.mjs" <<'JS'
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import * as stable from '@ancplua/qyl-semantic-conventions';
import * as all from '@ancplua/qyl-semantic-conventions/incubating';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
const pkg = JSON.parse(readFileSync(resolve(dirname(fileURLToPath(import.meta.resolve('@ancplua/qyl-semantic-conventions'))), '../package.json')));
assert.equal(pkg.version, process.argv[2]);
assert.equal(pkg.dependencies, undefined);
assert.equal(stable.SCHEMA_URL, `https://qyl.at/schemas/${process.argv[2]}`);
assert.equal(stable.ATTR_SERVICE_NAME, 'service.name');
assert.equal(all.ATTR_MCP_METHOD_NAME, 'mcp.method.name');
assert.equal(all.CPYTHON_GC_GENERATION_VALUE_GENERATION_0, 0);
assert.equal(all.EVENT_QYL_MCP_OPERATION, 'qyl.mcp.operation');
assert.equal(all.METRIC_MCP_SERVER_OPERATION_DURATION_UNIT, 's');
assert.equal(all.METRIC_MCP_SERVER_OPERATION_DURATION_DEFINITION.instrument, 'histogram');
assert.equal(stable.REGISTRY_SNAPSHOT_SHA256, all.REGISTRY_SNAPSHOT_SHA256);
assert.equal(stable.REGISTRY_SNAPSHOT_SHA256, process.argv[3]);
console.log(`TypeScript release consumer passed: ${pkg.version}; registry ${stable.REGISTRY_SNAPSHOT_SHA256}`);
JS
node "$temp/verify.mjs" "$version" "$expected_digest"
