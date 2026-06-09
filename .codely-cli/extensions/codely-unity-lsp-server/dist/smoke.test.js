import test from 'node:test';
import assert from 'node:assert/strict';
import { bootstrapLspRuntime } from './bootstrap.js';
import { UnityRoslynAdapter } from './adapters/unityRoslynAdapter.js';
test('UnityRoslynAdapter can be constructed', () => {
    const adapter = new UnityRoslynAdapter();
    assert.ok(adapter);
});
test('bootstrap result has stable shape', async () => {
    const result = await bootstrapLspRuntime();
    assert.equal(typeof result.ok, 'boolean');
    assert.equal(typeof result.message, 'string');
});
