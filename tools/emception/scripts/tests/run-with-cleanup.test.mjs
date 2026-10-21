import assert from "node:assert/strict";
import test from "node:test";
import { runWithCleanup } from "../run-with-cleanup.mjs";

function thrownBy(operation) {
  let threw = false;
  let thrown;
  try {
    operation();
  } catch (error) {
    threw = true;
    thrown = error;
  }
  assert.equal(threw, true, "An expected failure was not propagated");
  return thrown;
}

test("synchronous cleanup runs once after successful work", () => {
  const calls = [];
  runWithCleanup(() => calls.push("operation"), () => calls.push("cleanup"));
  assert.deepEqual(calls, ["operation", "cleanup"]);
});

test("cleanup preserves the original operation failure", () => {
  const failure = new Error("operation");
  let cleanups = 0;
  assert.equal(thrownBy(() => runWithCleanup(() => { throw failure; }, () => { cleanups += 1; })), failure);
  assert.equal(cleanups, 1);
});

test("cleanup failure after successful work is propagated unchanged", () => {
  const failure = new Error("cleanup");
  const calls = [];
  assert.equal(thrownBy(() => runWithCleanup(() => calls.push("operation"), () => {
    calls.push("cleanup");
    throw failure;
  })), failure);
  assert.deepEqual(calls, ["operation", "cleanup"]);
});

test("operation and cleanup failures are both retained in order", () => {
  const operation = new Error("operation");
  const cleanup = new Error("cleanup");
  const result = thrownBy(() => runWithCleanup(() => { throw operation; }, () => { throw cleanup; }));
  assert(result instanceof AggregateError);
  assert.deepEqual(result.errors, [operation, cleanup]);
  assert.equal(result.message, "Operation and cleanup both failed");
});

test("falsy thrown values still fail and always run cleanup", () => {
  for (const failure of [undefined, null, 0, ""]) {
    let cleanups = 0;
    assert.equal(thrownBy(() => runWithCleanup(() => { throw failure; }, () => { cleanups += 1; })), failure);
    assert.equal(cleanups, 1);
  }
});
