import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

import { resolvePnpmProcess } from "../../../apps/web/scripts/pnpm-process.mjs";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const nodePath = "C:\\Program Files\\nodejs\\node.exe";
const pnpmEntry = "C:\\owned path & literal\\pnpm.cjs";
const environment = { npm_execpath: pnpmEntry, OWNED_MARKER: "%literal% & value" };
const literalArguments = ["plain", "path with spaces", "owned & value", "%OWNED_MARKER%", "owned^literal", "[locale]/(auth)"];

function sourceFunction(source, name) {
  const start = source.indexOf(`function ${name}(`);
  assert.notEqual(start, -1);
  const end = source.indexOf("\n}", start);
  assert.notEqual(end, -1);
  return source.slice(start, end + 2);
}

function context(platform, status = 0) {
  const calls = [];
  const child = { status, exitCode: null, pid: 123456789 };
  const sandbox = {
    process: {
      platform,
      execPath: nodePath,
      env: environment,
      exit(code) { throw new Error(`owned exit ${code}`); },
      kill(pid, signal) { calls.push({ pid, signal }); },
    },
    shell: platform === "win32",
    spawn(command, args, options) {
      calls.push({ command, args: [...args], options });
      return child;
    },
    spawnSync(command, args, options) {
      calls.push({ command, args: Array.isArray(args) ? [...args] : args, options });
      return child;
    },
    resolvePnpmProcess(args, options) {
      return resolvePnpmProcess(args, {
        ...options, platform, nodePath,
        fileExists: (candidate) => candidate === pnpmEntry,
      });
    },
  };
  return { calls, child, sandbox };
}

for (const platform of ["win32", "linux"]) {
  for (const argument of literalArguments) {
    test(`dev preserves ${platform} literal argument ${argument}`, async () => {
      const source = await readFile(path.join(root, "scripts/dev.mjs"), "utf8");
      const { calls, child, sandbox } = context(platform);
      const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, sandbox);
      assert.equal(run("docker", ["compose", argument]), child);
      assert.equal(calls[0].command, "docker");
      assert.deepEqual(calls[0].args, ["compose", argument]);
      assert.equal(calls[0].options.shell, false);
      assert.equal(calls[0].options.detached, platform !== "win32");
      assert.equal(calls[0].options.stdio, "inherit");
    });
  }

  test(`dev resolves ${platform} pnpm without parsing arguments`, async () => {
    const source = await readFile(path.join(root, "scripts/dev.mjs"), "utf8");
    const { calls, sandbox } = context(platform);
    const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, sandbox);
    run("pnpm", literalArguments);
    assert.equal(calls[0].command, platform === "win32" ? nodePath : "pnpm");
    assert.deepEqual(calls[0].args, platform === "win32" ? [pnpmEntry, ...literalArguments] : literalArguments);
    assert.equal(calls[0].options.shell, false);
  });
}

test("dev Windows taskkill uses literal PID arguments without a shell", async () => {
  const source = await readFile(path.join(root, "scripts/dev.mjs"), "utf8");
  const { calls, child, sandbox } = context("win32");
  const killTree = vm.runInNewContext(`(${sourceFunction(source, "killTree")})`, sandbox);
  killTree(child);
  assert.equal(calls[0].command, "taskkill");
  assert.deepEqual(calls[0].args, ["/pid", "123456789", "/T", "/F"]);
  assert.equal(calls[0].options.shell, false);
});

test("dev skips exited children and preserves POSIX group termination", async () => {
  const source = await readFile(path.join(root, "scripts/dev.mjs"), "utf8");
  const { calls, child, sandbox } = context("linux");
  const killTree = vm.runInNewContext(`(${sourceFunction(source, "killTree")})`, sandbox);
  killTree({ ...child, exitCode: 0 });
  assert.equal(calls.length, 0);
  killTree(child);
  assert.deepEqual(calls, [{ pid: -123456789, signal: "SIGTERM" }]);
});

test("dev fails before spawning when Windows pnpm cannot be resolved", async () => {
  const source = await readFile(path.join(root, "scripts/dev.mjs"), "utf8");
  const { calls, sandbox } = context("win32");
  sandbox.resolvePnpmProcess = () => { throw new Error("owned missing pnpm"); };
  const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, sandbox);
  assert.throws(() => run("pnpm", ["run", "dev"]), /owned missing pnpm/);
  assert.equal(calls.length, 0);
});

for (const platform of ["win32", "linux"]) {
  test(`library builder preserves ${platform} target argument vectors`, async () => {
    const source = await readFile(path.join(root, "tools/emception/scripts/ensure-emception-libs.mjs"), "utf8");
    const { calls, sandbox } = context(platform);
    const targetsStart = source.indexOf("const targets = [");
    const targetsEnd = source.indexOf("\n];", targetsStart);
    assert.notEqual(targetsStart, -1);
    assert.notEqual(targetsEnd, -1);
    sandbox.join = path.join;
    sandbox.emceptionRoot = "/owned emception & literal";
    const targets = vm.runInNewContext(`${source.slice(targetsStart, targetsEnd + 3)}\n targets`, sandbox);
    assert.equal(targets.length, 5);
    const callStart = source.indexOf("const result = spawnSync(");
    const invocationStart = source.lastIndexOf("const invocation = ", callStart);
    const callEnd = source.indexOf("\n        if (result.status", callStart);
    assert.notEqual(callStart, -1);
    assert.notEqual(callEnd, -1);
    for (const [index, target] of targets.entries()) {
      vm.runInNewContext(`${source.slice(invocationStart === -1 ? callStart : invocationStart, callEnd)}\n result`, { ...sandbox, target });
      const call = calls[index];
      const args = ["run", index === 0 ? "build:lib" : "build"];
      assert.equal(call.command, platform === "win32" ? nodePath : "pnpm");
      assert.deepEqual(call.args, platform === "win32" ? [pnpmEntry, ...args] : args);
      assert.equal(call.options.cwd, target.cwd);
      assert.equal(call.options.stdio, "inherit");
      assert.equal(call.options.shell, false);
    }
  });

  for (const status of [0, 7, null]) {
    test(`dependency builder retains ${platform} status ${status}`, async () => {
      const source = await readFile(path.join(root, "apps/web/scripts/build-emception-dependencies.mjs"), "utf8");
      const { calls, sandbox } = context(platform, status);
      const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, sandbox);
      const cwd = "/owned build & literal";
      if (status === 0) run(literalArguments, cwd);
      else assert.throws(() => run(literalArguments, cwd), new RegExp(`owned exit ${status ?? 1}`));
      assert.equal(calls[0].command, platform === "win32" ? nodePath : "pnpm");
      assert.deepEqual(calls[0].args, platform === "win32" ? [pnpmEntry, ...literalArguments] : literalArguments);
      assert.equal(calls[0].options.cwd, cwd);
      assert.equal(calls[0].options.stdio, "inherit");
      assert.equal(calls[0].options.shell, false);
    });
  }

  test(`coding cycle preserves ${platform} web process contract`, async () => {
    const source = await readFile(path.join(root, "apps/web/scripts/coding-cycle-browser-e2e.mjs"), "utf8");
    const { calls, child, sandbox } = context(platform);
    const webStart = source.indexOf("  const web = spawn(");
    const invocationStart = source.lastIndexOf("  const webInvocation = ", webStart);
    const end = source.indexOf("\n  children.push({ proc: web", webStart);
    assert.notEqual(webStart, -1);
    assert.notEqual(end, -1);
    const start = invocationStart === -1 ? webStart : invocationStart;
    sandbox.WEB_PORT = 3210;
    sandbox.WEB_DIR = "/owned web & literal";
    sandbox.webEnv = { OWNED_WEB: "literal & environment" };
    sandbox.envArrayToObject = (value) => value;
    const web = vm.runInNewContext(`${source.slice(start, end)}\n web`, sandbox);
    assert.equal(web, child);
    assert.equal(calls.length, 1);
    const args = ["exec", "next", "dev", "--webpack", "--port", "3210"];
    assert.equal(calls[0].command, platform === "win32" ? nodePath : "pnpm");
    assert.deepEqual(calls[0].args, platform === "win32" ? [pnpmEntry, ...args] : args);
    assert.equal(calls[0].options.cwd, sandbox.WEB_DIR);
    assert.equal(calls[0].options.env.OWNED_MARKER, environment.OWNED_MARKER);
    assert.equal(calls[0].options.env.OWNED_WEB, sandbox.webEnv.OWNED_WEB);
    assert.equal(calls[0].options.detached, platform !== "win32");
    assert.deepEqual([...calls[0].options.stdio], ["ignore", "pipe", "pipe"]);
    assert.equal(calls[0].options.shell, false);
  });
}
