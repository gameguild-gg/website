// withCancellation verification — race semantics.

import assert from 'node:assert/strict';
import test from 'node:test';

import {
    TimeoutError,
    withCancellation,
    withTimeoutOrThrow,
} from '../dist/index.js';

const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

test('ok path: op resolves before timeout', async () => {
    const op = wait(5).then(() => 42);
    const out = await withCancellation(op, { timeoutMs: 100 });
    assert.equal(out.kind, 'ok');
    assert.equal(out.value, 42);
    assert.ok(out.durationMs >= 0);
});

test('timeout path: deadline beats op; cleanup ran exactly once', async () => {
    let cleanupCalls = 0;
    const op = wait(200).then(() => 'late');
    const out = await withCancellation(op, {
        timeoutMs: 20,
        cleanup: () => { cleanupCalls += 1; },
    });
    assert.equal(out.kind, 'timeout');
    if (out.kind === 'timeout') {
        assert.equal(out.timeoutMs, 20);
        assert.ok(out.durationMs >= 20);
    }
    assert.equal(cleanupCalls, 1);
});

test('abort path: pre-aborted signal resolves immediately', async () => {
    const ac = new AbortController();
    ac.abort('manual');
    let cleanupCalls = 0;
    const out = await withCancellation(wait(1000), {
        signal: ac.signal,
        cleanup: () => { cleanupCalls += 1; },
    });
    assert.equal(out.kind, 'abort');
    if (out.kind === 'abort') {
        assert.equal(out.reason, 'manual');
    }
    assert.equal(cleanupCalls, 1);
});

test('abort path: signal fires mid-flight', async () => {
    const ac = new AbortController();
    setTimeout(() => ac.abort(new Error('user-cancel')), 10);
    let cleanupCalls = 0;
    const out = await withCancellation(wait(1000), {
        signal: ac.signal,
        cleanup: () => { cleanupCalls += 1; },
    });
    assert.equal(out.kind, 'abort');
    if (out.kind === 'abort') {
        assert.ok(out.reason instanceof Error);
        assert.equal(out.reason.message, 'user-cancel');
    }
    assert.equal(cleanupCalls, 1);
});

test('op rejection re-throws verbatim (no timeout/abort)', async () => {
    const err = new Error('tool crashed');
    await assert.rejects(
        withCancellation(Promise.reject(err), { timeoutMs: 100 }),
        (e) => e === err,
    );
});

test('cleanup errors are swallowed; outcome still resolves', async () => {
    const out = await withCancellation(wait(100), {
        timeoutMs: 5,
        cleanup: () => { throw new Error('cleanup boom'); },
    });
    assert.equal(out.kind, 'timeout');
});

test('cleanup not invoked on the ok path', async () => {
    let cleanupCalls = 0;
    await withCancellation(wait(5).then(() => 1), {
        timeoutMs: 1000,
        cleanup: () => { cleanupCalls += 1; },
    });
    // Give any spurious timer one more macrotask to fire — it must not.
    await wait(10);
    assert.equal(cleanupCalls, 0);
});

test('withTimeoutOrThrow returns value on ok', async () => {
    const v = await withTimeoutOrThrow(wait(1).then(() => 'ok'), { timeoutMs: 50 });
    assert.equal(v, 'ok');
});

test('withTimeoutOrThrow throws TimeoutError on deadline', async () => {
    await assert.rejects(
        withTimeoutOrThrow(wait(100), { timeoutMs: 5 }),
        TimeoutError,
    );
});

test('withTimeoutOrThrow re-throws abort reason', async () => {
    const ac = new AbortController();
    setTimeout(() => ac.abort('stop'), 5);
    await assert.rejects(
        withTimeoutOrThrow(wait(1000), { signal: ac.signal }),
        (e) => e === 'stop',
    );
});

test('timeoutMs<=0 disables the timer', async () => {
    const out = await withCancellation(wait(5).then(() => 'done'), { timeoutMs: 0 });
    assert.equal(out.kind, 'ok');
});

const deferred = () => {
    let resolve;
    const promise = new Promise((complete) => { resolve = complete; });
    return { promise, resolve };
};

test('timeout retains its outcome while asynchronous cleanup is pending', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const operation = deferred();
    const cleanup = deferred();
    let cleanupCalls = 0;
    let elapsed = 0;
    const result = withCancellation(operation.promise, {
        timeoutMs: 20,
        now: () => elapsed,
        cleanup: () => { cleanupCalls += 1; return cleanup.promise; },
    });
    elapsed = 20;
    t.mock.timers.tick(20);
    operation.resolve('completed after cancellation started');
    await Promise.resolve();
    cleanup.resolve();
    const outcome = await result;
    assert.equal(outcome.kind, 'timeout');
    assert.equal(cleanupCalls, 1);
});

test('timeout followed by abort invokes asynchronous cleanup exactly once', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const operation = deferred();
    const cleanup = deferred();
    const controller = new AbortController();
    let cleanupCalls = 0;
    let elapsed = 0;
    const result = withCancellation(operation.promise, {
        timeoutMs: 20,
        signal: controller.signal,
        now: () => elapsed,
        cleanup: () => { cleanupCalls += 1; return cleanup.promise; },
    });
    elapsed = 20;
    t.mock.timers.tick(20);
    controller.abort('later abort');
    cleanup.resolve();
    const outcome = await result;
    assert.equal(outcome.kind, 'timeout');
    assert.equal(cleanupCalls, 1);
});

test('an operation that completes first does not invoke cancellation cleanup', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const controller = new AbortController();
    let cleanupCalls = 0;
    const outcome = await withCancellation(Promise.resolve('completed'), {
        timeoutMs: 20,
        signal: controller.signal,
        cleanup: () => { cleanupCalls += 1; },
    });
    t.mock.timers.tick(20);
    controller.abort('later abort');
    assert.equal(outcome.kind, 'ok');
    assert.equal(cleanupCalls, 0);
});

test('an early timer callback waits for the actual elapsed deadline', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const operation = deferred();
    let cleanupCalls = 0;
    let elapsed = 0;
    const result = withCancellation(operation.promise, {
        timeoutMs: 20,
        now: () => elapsed,
        cleanup: () => { cleanupCalls += 1; },
    });
    elapsed = 19;
    t.mock.timers.tick(20);
    assert.equal(cleanupCalls, 0);
    elapsed = 20;
    t.mock.timers.tick(1);
    const outcome = await result;
    assert.equal(outcome.kind, 'timeout');
    assert.equal(outcome.durationMs, 20);
    assert.equal(cleanupCalls, 1);
});

for (const timeoutMs of [Infinity, NaN]) {
    test(`non-finite timeout ${timeoutMs} is rejected`, async () => {
        await assert.rejects(withCancellation(Promise.resolve('completed'), { timeoutMs }), RangeError);
    });
}

test('long deadlines are scheduled without overflowing the timer range', async (t) => {
    t.mock.timers.enable({ apis: ['setTimeout'] });
    const timeoutMs = 2_147_483_648;
    const operation = deferred();
    let cleanupCalls = 0;
    let elapsed = 0;
    const result = withCancellation(operation.promise, {
        timeoutMs,
        now: () => elapsed,
        cleanup: () => { cleanupCalls += 1; },
    });
    elapsed = timeoutMs - 1;
    t.mock.timers.tick(timeoutMs - 1);
    assert.equal(cleanupCalls, 0);
    elapsed = timeoutMs;
    t.mock.timers.tick(1);
    const outcome = await result;
    assert.equal(outcome.kind, 'timeout');
    assert.equal(outcome.durationMs, timeoutMs);
    assert.equal(cleanupCalls, 1);
});
