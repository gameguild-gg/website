import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { readFile } from "node:fs/promises";
import test from "node:test";
import vm from "node:vm";

// Extract only run: importing dev-stop would execute its service-stopping main.
const source = await readFile(new URL("../../dev-stop.mjs", import.meta.url), "utf8");
const start = source.indexOf("function run(");
assert.notEqual(start, -1);
const end = source.indexOf("\n}", start);
assert.notEqual(end, -1);
const runSource = source.slice(start, end + 2);
const repositoryRoot = "C:\\owned repository & literal\\Game Guild";

function fixture(platform) {
  const calls = [];
  const child = new EventEmitter();
  const run = vm.runInNewContext(
    "(" + runSource + ")",
    {
      repositoryRoot,
      process: { platform },
      spawn(command, args, options) {
        calls.push({ command, args, options });
        return child;
      },
    },
    { timeout: 1_000 },
  );
  return { run, child, calls };
}

const literalArguments = [
  "path with spaces/compose.yaml",
  "owned-value & echo OWNED_SHELL_SENTINEL",
  "%OWNED_PROCESS_MARKER%",
  "owned^literal",
  "owned;literal",
  "owned$(literal)",
];

for (const platform of ["win32", "linux"]) {
  for (const argument of literalArguments) {
    test(platform + " keeps Docker arguments literal: " + argument, async () => {
      const { run, child, calls } = fixture(platform);
      const args = ["compose", "-f", argument, "down", "--remove-orphans"];
      const operation = run("docker", args);
      assert.equal(calls.length, 1);
      assert.equal(calls[0].command, "docker");
      assert.equal(calls[0].args, args);
      assert.equal(calls[0].options.cwd, repositoryRoot);
      assert.equal(calls[0].options.shell, false);
      assert.equal(calls[0].options.windowsHide, true);
      assert.equal(calls[0].options.stdio, "inherit");
      child.emit("exit", 0, null);
      await operation;
    });
  }

  test(platform + " resolves a successful Docker exit", async () => {
    const { run, child } = fixture(platform);
    const operation = run("docker", ["compose", "version"]);
    child.emit("exit", 0, null);
    await operation;
  });

  test(platform + " rejects a failed Docker exit", async () => {
    const { run, child } = fixture(platform);
    const operation = run("docker", ["compose", "version"]);
    const rejected = assert.rejects(operation, /docker failed \(exit 42\)/);
    child.emit("exit", 42, null);
    await rejected;
  });

  test(platform + " rejects a terminated Docker process", async () => {
    const { run, child } = fixture(platform);
    const operation = run("docker", ["compose", "version"]);
    const rejected = assert.rejects(operation, /docker failed \(SIGTERM\)/);
    child.emit("exit", null, "SIGTERM");
    await rejected;
  });

  test(platform + " preserves a Docker spawn error", async () => {
    const { run, child } = fixture(platform);
    const operation = run("docker", ["compose", "version"]);
    const expected = new Error("owned Docker executable missing");
    const rejected = assert.rejects(operation, error => error === expected);
    child.emit("error", expected);
    await rejected;
  });
}
