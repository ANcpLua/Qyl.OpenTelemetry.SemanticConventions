import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { test } from 'node:test';

const root = resolve(import.meta.dirname, '../..');
test('packed exports preserve constants, units, tiers and C# parity', () => {
  const temp = mkdtempSync(resolve(tmpdir(), 'qyl-semconv-'));
  try {
    const output = JSON.parse(execFileSync('npm', ['pack', './typescript', '--json', '--pack-destination', temp], {cwd:root,encoding:'utf8'}));
    const packages = Array.isArray(output) ? output : Object.values(output);
    assert.equal(packages.length, 1);
    const [packed] = packages;
    assert.equal(packed.name, '@ancplua/qyl-semantic-conventions');
    assert.equal(packed.version, JSON.parse(readFileSync(resolve(root, 'typescript/package.json'))).version);
    assert.equal(typeof packed.filename, 'string');
    execFileSync('npm', ['install', '--prefix', temp, '--ignore-scripts', '--no-audit', '--no-fund', resolve(temp, packed.filename)], {encoding:'utf8'});
    const check = `
import assert from 'node:assert/strict';
import * as stable from '@ancplua/qyl-semantic-conventions';
import * as all from '@ancplua/qyl-semantic-conventions/incubating';
assert.equal(stable.ATTR_SERVICE_NAME, 'service.name');
assert.equal(stable.NETWORK_TRANSPORT_VALUE_TCP, 'tcp');
assert.equal(all.ATTR_MCP_METHOD_NAME, 'mcp.method.name');
assert.equal(all.CLOUD_PLATFORM_VALUE_AZURE_VM, 'azure.vm');
assert.ok(!Object.values(all).includes('azure_vm'));
assert.equal(all.GEN_AI_OPERATION_NAME_VALUE_EXECUTE_TOOL, 'execute_tool');
assert.equal(all.ATTR_QYL_MCP_EXECUTION_ID, 'qyl.mcp.execution.id');
assert.equal(all.EVENT_QYL_MCP_OPERATION, 'qyl.mcp.operation');
assert.equal(all.METRIC_MCP_SERVER_OPERATION_DURATION_UNIT, 's');
assert.equal(all.METRIC_MCP_SERVER_OPERATION_DURATION_DEFINITION.instrument, 'histogram');
assert.equal(stable.ATTR_MCP_METHOD_NAME, undefined);
for (const [key,value] of Object.entries(stable)) assert.deepEqual(all[key], value);
console.log(JSON.stringify({stable, incubating: all}));
`;
    const exported = execFileSync('node', ['--input-type=module', '-e', check], {cwd:temp,encoding:'utf8'});
    const parity = resolve(temp, 'exports.json');
    writeFileSync(parity, exported);
    execFileSync('python3', ['scripts/verify-typescript-parity.py', parity], {cwd:root,encoding:'utf8'});
    const pkg = JSON.parse(readFileSync(resolve(temp, 'node_modules/@ancplua/qyl-semantic-conventions/package.json')));
    assert.equal(pkg.sideEffects, false);
    assert.equal(pkg.dependencies, undefined);
    for (const entry of Object.values(pkg.exports)) assert.ok(readFileSync(resolve(temp, 'node_modules/@ancplua/qyl-semantic-conventions', entry.types), 'utf8').includes('export declare const ATTR_'));
  } finally { rmSync(temp, {recursive:true,force:true}); }
});
