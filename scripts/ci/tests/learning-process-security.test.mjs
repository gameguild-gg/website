import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

import { resolvePnpmProcess } from "../../../apps/web/scripts/pnpm-process.mjs";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const nodePath = "C:\\Program Files\\nodejs\\node.exe";

function sourceFunction(source, name) {
  const start = source.indexOf(`function ${name}(`);
  assert.notEqual(start, -1);
  // These functions end at column zero; nested functions do not.
  const end = source.indexOf("\n}", start);
  assert.notEqual(end, -1);
  return source.slice(start, end + 2);
}

async function ownedDirectory() {
  const parent = path.resolve(tmpdir());
  const directory = await mkdtemp(
    path.join(parent, "gameguild-process-proof-"),
  );
  return {
    directory,
    async cleanup() {
      assert.equal(path.dirname(path.resolve(directory)), parent);
      assert(path.basename(directory).startsWith("gameguild-process-proof-"));
      await rm(directory, { recursive: true, force: true });
    },
  };
}

function waitForExit(child) {
  return new Promise((resolve, reject) => {
    child.once("error", reject);
    child.once("exit", (code, signal) => {
      if (code === 0) resolve();
      else reject(new Error(`Owned child failed: ${signal ?? code}`));
    });
  });
}

const argumentsToPreserve = [
  "plain-owned-argument",
  "path with spaces",
  "src/app/[locale]/(auth)/**/page.tsx",
  "owned-value & echo OWNED_SHELL_SENTINEL",
  "%OWNED_PROCESS_MARKER%",
  "owned^literal",
];
for (const relative of [
  "scripts/dev-fast.mjs",
]) {
  for (const argument of argumentsToPreserve) {
    test(`${relative} preserves literal child argument ${argument}`, async () => {
      const owned = await ownedDirectory();
      try {
        const entry = path.join(owned.directory, "argv.cjs");
        const result = path.join(owned.directory, "argv.json");
        await writeFile(
          entry,
          "require('node:fs').writeFileSync(process.env.OWNED_ARGV_OUTPUT, JSON.stringify(process.argv.slice(2)));",
        );
        const environment = {
          ...process.env,
          OWNED_ARGV_OUTPUT: result,
          OWNED_PROCESS_MARKER: "expanded-owned-marker",
        };
        const calls = [];
        const source = await readFile(path.join(root, relative), "utf8");
        const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, {
          spawn(command, args, options) {
            calls.push({ command, args, options });
            return spawn(command, args, options);
          },
          process: { platform: "win32" },
          shell: true,
          children: new Set(),
          runtimeEnv: environment,
          buildEnv: environment,
          webRoot: owned.directory,
          pnpmProcess: await resolvePnpmProcess([], { platform: "linux" }),
        });
        const operation = run(process.execPath, [entry, argument]);
        await waitForExit(operation);
        const actual = JSON.parse(await readFile(result, "utf8"));
        assert.deepEqual(actual, [argument]);
        assert.equal(calls.length, 1);
        assert.equal(calls[0].options.shell, false);
        assert.equal(calls[0].options.windowsHide, true);
      } finally {
        await owned.cleanup();
      }
    });
  }
}

for (const relative of [
  "scripts/dev-fast.mjs",
]) {
  test(`${relative} executes a pnpm entry point containing spaces and metacharacters`, async () => {
    const owned = await ownedDirectory();
    try {
      const cliDirectory = path.join(owned.directory, "owned cli & literal");
      await mkdir(cliDirectory);
      const entry = path.join(cliDirectory, "pnpm.cjs");
      const result = path.join(owned.directory, "pnpm-argv.json");
      await writeFile(
        entry,
        "require('node:fs').writeFileSync(process.env.OWNED_ARGV_OUTPUT, JSON.stringify(process.argv.slice(2)));",
      );
      const environment = {
        ...process.env,
        npm_execpath: entry,
        OWNED_ARGV_OUTPUT: result,
      };
      const source = await readFile(path.join(root, relative), "utf8");
      const run = vm.runInNewContext(`(${sourceFunction(source, "run")})`, {
        spawn,
        process: { platform: "win32", env: environment },
        children: new Set(),
        runtimeEnv: environment,
        buildEnv: environment,
        webRoot: owned.directory,
        pnpmProcess: await resolvePnpmProcess([], {
          platform: "win32",
          env: environment,
        }),
      });
      const args = ["owned argument & literal", "%OWNED_PROCESS_MARKER%"];
      const operation = run("pnpm", args);
      await waitForExit(operation);
      assert.deepEqual(JSON.parse(await readFile(result, "utf8")), args);
    } finally {
      await owned.cleanup();
    }
  });
}

test("Windows taskkill invokes its native executable without a shell", async () => {
  const source = await readFile(
    path.join(root, "scripts/dev-fast.mjs"),
    "utf8",
  );
  const calls = [];
  const killTree = vm.runInNewContext(
    `(${sourceFunction(source, "killTree")})`,
    {
      process: { platform: "win32" },
      spawn(command, args, options) {
        calls.push({ command, args: [...args], options });
      },
    },
  );
  killTree({ exitCode: null, pid: 123456789 });
  assert.equal(calls.length, 1);
  assert.equal(calls[0].command, "taskkill");
  assert.deepEqual(calls[0].args, ["/pid", "123456789", "/T", "/F"]);
  assert.equal(calls[0].options.shell, false);
  assert.equal(calls[0].options.windowsHide, true);
});

test("Unix keeps the native pnpm command and literal arguments", async () => {
  const args = ["exec", "owned argument & literal"];
  assert.deepEqual(await resolvePnpmProcess(args, {
    platform: "linux",
    fileExists() {
      assert.fail("Unix must not probe Windows executable paths");
    },
  }), {
    command: "pnpm",
    args,
  });
});

test("Windows uses the pnpm JavaScript entry point supplied by pnpm", async () => {
  const entry = "C:\\owned path & literal\\pnpm.cjs";
  const args = ["run", "owned argument"];
  assert.deepEqual(
    await resolvePnpmProcess(args, {
      platform: "win32",
      nodePath,
      env: { npm_execpath: entry },
      fileExists: (candidate) => candidate === entry,
    }),
    { command: nodePath, args: [entry, ...args] },
  );
});

for (const [kind, entry] of [
  ["native executable", "C:\\owned path\\pnpm.exe"],
  ["global pnpm", "C:\\owned path\\node_modules\\pnpm\\bin\\pnpm.cjs"],
  ["Corepack pnpm", "C:\\owned path\\node_modules\\corepack\\dist\\pnpm.js"],
  ["standalone directory", "C:\\owned path\\pnpm.cjs"],
]) {
  test(`Windows resolves ${kind} through PATH without a shell`, async () => {
    const args = ["--version"];
    const actual = await resolvePnpmProcess(args, {
      platform: "win32",
      nodePath,
      env: { Path: "C:\\owned path" },
      fileExists: (candidate) => candidate === entry,
    });
    assert.deepEqual(actual, {
      command: entry.endsWith(".exe") ? entry : nodePath,
      args: entry.endsWith(".exe") ? args : [entry, ...args],
    });
  });
}

for (const entry of [
  "pnpm.cmd",
  "C:\\owned\\npm-cli.js",
  "relative/pnpm.cjs",
]) {
  test(`Windows never runs an unrelated or non-absolute npm_execpath: ${entry}`, async () => {
    await assert.rejects(
      () =>
        resolvePnpmProcess([], {
          platform: "win32",
          nodePath,
          env: { npm_execpath: entry },
          fileExists: (candidate) => candidate === entry,
        }),
      /Cannot find a pnpm executable/,
    );
  });
}

test("Windows fails closed when no native pnpm or JavaScript entry point exists", async () => {
  await assert.rejects(
    () =>
      resolvePnpmProcess([], {
        platform: "win32",
        nodePath,
        env: {},
        fileExists: () => false,
      }),
    /Cannot find a pnpm executable/,
  );
});

test("Windows PATH precedence matches Node for differently cased environment keys", async () => {
  const correct = "C:\\lexical-first\\pnpm.exe";
  const incorrect = "C:\\insertion-first\\pnpm.exe";
  assert.deepEqual(
    await resolvePnpmProcess([], {
      platform: "win32",
      nodePath,
      env: { Path: "C:\\insertion-first", PATH: "C:\\lexical-first" },
      fileExists: (candidate) => [correct, incorrect].includes(candidate),
    }),
    { command: correct, args: [] },
  );
});

test("Windows waits for the managed entry probe before considering PATH", async () => {
  const entry = "C:\\managed path\\pnpm.cjs";
  const probe = Promise.withResolvers();
  const checked = [];
  const operation = resolvePnpmProcess(["--version"], {
    platform: "win32",
    nodePath,
    env: { npm_execpath: entry, PATH: "C:\\later path" },
    fileExists(candidate) {
      checked.push(candidate);
      return probe.promise;
    },
  });
  assert.deepEqual(checked, [entry]);
  probe.resolve(true);
  assert.deepEqual(await operation, {
    command: nodePath,
    args: [entry, "--version"],
  });
  assert.deepEqual(checked, [entry]);
});

test("Windows awaits false probes and preserves directory and entry precedence", async () => {
  const directory = "C:\\first path";
  const entry = path.win32.join(directory, "node_modules/corepack/dist/pnpm.js");
  const later = "C:\\later path\\pnpm.exe";
  const checked = [];
  const actual = await resolvePnpmProcess([], {
    platform: "win32",
    nodePath,
    env: { PATH: `${directory};C:\\later path` },
    async fileExists(candidate) {
      checked.push(candidate);
      return candidate === entry || candidate === later;
    },
  });
  assert.deepEqual(actual, { command: nodePath, args: [entry] });
  assert.deepEqual(checked, [
    path.win32.join(directory, "pnpm.exe"),
    path.win32.join(directory, "node_modules/pnpm/bin/pnpm.cjs"),
    entry,
  ]);
});

for (const [kind, env, entry] of [
  ["PNPM_HOME", { PNPM_HOME: "C:\\owned home" }, "C:\\owned home\\pnpm.exe"],
  ["the Node directory", {}, "C:\\Program Files\\nodejs\\pnpm.cjs"],
]) {
  test(`Windows falls back to ${kind} after a missing managed entry`, async () => {
    const actual = await resolvePnpmProcess([], {
      platform: "win32",
      nodePath,
      env: { ...env, npm_execpath: "C:\\missing\\pnpm.cjs" },
      fileExists: async (candidate) => candidate === entry,
    });
    assert.deepEqual(actual, {
      command: entry.endsWith(".exe") ? entry : nodePath,
      args: entry.endsWith(".exe") ? [] : [entry],
    });
  });
}

test("Windows fails closed when every asynchronous existence probe returns false", async () => {
  await assert.rejects(
    resolvePnpmProcess([], {
      platform: "win32",
      nodePath,
      env: { PATH: "C:\\missing", PNPM_HOME: "C:\\also missing" },
      fileExists: async () => false,
    }),
    /Cannot find a pnpm executable/,
  );
});

test("Windows propagates an injected probe failure without choosing a fallback", async () => {
  const failure = new Error("owned probe failure");
  await assert.rejects(
    resolvePnpmProcess([], {
      platform: "win32",
      nodePath,
      env: { PATH: "C:\\owned path" },
      fileExists: () => Promise.reject(failure),
    }),
    (error) => error === failure,
  );
});

test("Development startup awaits pnpm discovery before starting any child", async () => {
  const source = await readFile(path.join(root, "scripts/dev-fast.mjs"), "utf8");
  const initialization = "const pnpmProcess = await resolvePnpmProcess([], { env: runtimeEnv });";
  const discovery = source.indexOf(initialization);
  assert.notEqual(discovery, -1);
  assert(discovery < source.indexOf('const compose = run("docker"'));
  assert(discovery < source.indexOf('const api = spawn('));
});
