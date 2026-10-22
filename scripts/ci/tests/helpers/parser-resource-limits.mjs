import assert from "node:assert/strict";
import { createRequire } from "node:module";
import { parentPort, workerData } from "node:worker_threads";

const require = createRequire(import.meta.url);
const { packageName, packageRoot } = workerData;

if (packageName === "toml") {
  const toml = require(packageRoot);
  for (const literal of [
    "9".repeat(40_000),
    `0x${"f".repeat(40_000)}`,
    `0o${"7".repeat(40_000)}`,
    `0b${"1".repeat(40_000)}`,
  ]) {
    assert.throws(() => toml.parse(`value = ${literal}`), /64-bit/);
  }

  const tableCount = 20_000;
  const tables = toml.parse(Array.from({ length: tableCount }, (_, i) => `[[item${i}]]\nvalue = ${i}\n`).join(""));
  assert.equal(Object.keys(tables).length, tableCount);
  assert.equal(tables.item0[0].value, 0);
  assert.equal(tables.item19999[0].value, tableCount - 1);

  const depth = 20_000;
  const path = Array.from({ length: depth }, () => "part").join(".");
  for (const input of [`${path}.value = 1`, `[${path}]\nvalue = 1`]) {
    let table = toml.parse(input);
    for (let i = 0; i < depth; i++) table = table.part;
    assert.equal(table.value, 1);
  }
} else if (packageName === "ip-address") {
  const { Address4, Address6 } = require(packageRoot);
  for (const terminator of ["\n", "\r", "\u2028", "\u2029"]) {
    const address = `fe80::1${"%".repeat(100_000)}${terminator}invalid`;
    assert.throws(() => new Address6(address));
  }
  assert.throws(() => Address4.fromArpa(`${"1.".repeat(40_000)}in-addr.arpa.`), /at most 32 characters/);
} else {
  assert.fail(`Unexpected parser package: ${packageName}`);
}

parentPort.postMessage(packageName);
