import assert from 'node:assert/strict';
import { registerHooks } from 'node:module';
import test from 'node:test';

// Load the real compiled bridge with only the browser display dependency replaced.
// The supplied terminal fixture implements the external event boundary; byte queues,
// exclusive routing and subscription disposal execute in the real TTYBridge.
const bridgeUrl = new URL('../../xterm/dist/bridge.js?tty-input-contracts', import.meta.url);
const displayStub = 'data:text/javascript,export class Terminal { constructor() { throw new Error("Input contracts must reuse the supplied terminal"); } }';
const hooks = registerHooks({
  resolve(specifier, context, nextResolve) {
    if (specifier === '@xterm/xterm' && context.parentURL === bridgeUrl.href) {
      return { url: displayStub, shortCircuit: true };
    }
    return nextResolve(specifier, context);
  },
});
let TTYBridge;
try {
  ({ TTYBridge } = await import(bridgeUrl.href));
} finally {
  hooks.deregister();
}

function terminalFixture() {
  const listeners = new Set();
  const handles = [];
  const output = [];
  const terminal = {
    writeln(value) { output.push(value + '\r\n'); },
    write(value) { output.push(value); },
    clear() { output.length = 0; },
    onData(listener) {
      assert.equal(this, terminal, 'Terminal methods must retain their receiver');
      listeners.add(listener);
      const handle = { dispose() { listeners.delete(listener); } };
      handles.push(handle);
      return handle;
    },
  };
  return {
    terminal,
    handles,
    emit(data) {
      for (const listener of [...listeners]) listener(data);
    },
  };
}

test('raw input subscriptions preserve data and return the terminal disposable', () => {
  const fixture = terminalFixture();
  const tty = new TTYBridge(fixture.terminal);
  const received = [];
  const subscription = tty.onData((data) => received.push(data));
  assert.equal(subscription, fixture.handles.at(-1));
  fixture.emit('a\r');
  assert.deepEqual(received, ['a\r']);
  assert.equal(tty.readByte(), 97);
  assert.equal(tty.readByte(), 13);
  subscription.dispose();
  fixture.emit('b');
  assert.deepEqual(received, ['a\r']);
  assert.equal(tty.readByte(), 98, 'Disposing the observer must retain bridge routing');
});

test('normal and exclusive byte routing precedes raw input observers', () => {
  const fixture = terminalFixture();
  const tty = new TTYBridge(fixture.terminal);
  const received = [];
  tty.onData((data) => {
    const exclusive = tty.isExclusiveStdin;
    const byte = exclusive ? tty.readByteExclusive() : tty.readByte();
    received.push({ data, exclusive, byte });
  });
  fixture.emit('a');
  tty.enterExclusiveStdin();
  fixture.emit('b');
  tty.exitExclusiveStdin();
  fixture.emit('c');
  assert.deepEqual(received, [
    { data: 'a', exclusive: false, byte: 97 },
    { data: 'b', exclusive: true, byte: 98 },
    { data: 'c', exclusive: false, byte: 99 },
  ]);
});

test('exclusive input does not resolve a pending normal reader', async () => {
  const fixture = terminalFixture();
  const tty = new TTYBridge(fixture.terminal);
  let normalResolved = false;
  const normal = tty.readByte();
  assert.ok(normal instanceof Promise);
  normal.then(() => { normalResolved = true; });
  tty.enterExclusiveStdin();
  const exclusive = tty.readByteExclusive();
  const observedModes = [];
  tty.onData(() => observedModes.push(tty.isExclusiveStdin));
  fixture.emit('x');
  assert.equal(await exclusive, 120);
  await Promise.resolve();
  assert.equal(normalResolved, false);
  tty.exitExclusiveStdin();
  fixture.emit('y');
  assert.equal(await normal, 121);
  assert.deepEqual(observedModes, [true, false]);
});
