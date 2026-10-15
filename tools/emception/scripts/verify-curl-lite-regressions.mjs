import { createHash } from "node:crypto";
import { spawnSync } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  readFileSync,
  realpathSync,
  writeFileSync,
  rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../", import.meta.url));
const library = path.join(root, "toolchain/overlays/libcurl-lite");
const lock = JSON.parse(
  readFileSync(path.join(root, "toolchain/toolchain.lock.json"), "utf8"),
);
const expectedVersion = lock.tools.emsdk.version;
const sdk = process.env.EMSDK ?? path.join(root, ".cache/toolchain/emsdk");
const emcc = path.join(sdk, "upstream/emscripten/emcc");
if (!existsSync(emcc)) {
  throw new Error(
    "The pinned Emscripten SDK must be installed before running curl-lite regressions",
  );
}
const environment = {
  ...process.env,
  EMSDK: sdk,
  EM_CONFIG: process.env.EM_CONFIG ?? path.join(sdk, ".emscripten"),
};

function run(command, args, capture = false) {
  const result = spawnSync(command, args, {
    env: environment,
    encoding: "utf8",
    stdio: capture ? "pipe" : "inherit",
  });
  if (result.error) throw result.error;
  if (result.status !== 0)
    throw new Error(`Command failed (${result.status}): ${command}`);
  return result.stdout;
}

const emccCommand =
  process.platform === "win32" ? (process.env.EMSDK_PYTHON ?? "python") : emcc;
const emccPrefix = process.platform === "win32" ? [emcc] : [];
const versionOutput = run(emccCommand, [...emccPrefix, "--version"], true);
const actualVersion = /\)\s+([0-9]+\.[0-9]+\.[0-9]+)\b/.exec(
  versionOutput,
)?.[1];
if (actualVersion !== expectedVersion) {
  throw new Error(
    `Emscripten ${actualVersion ?? "unknown"} does not match pinned ${expectedVersion}`,
  );
}
process.stdout.write(versionOutput);

const temporaryBase = realpathSync(tmpdir());
const prefix = "emception-curl-regressions-";
const directory = mkdtempSync(path.join(temporaryBase, prefix));
try {

  const baselineRevision = process.env.CURL_LITE_BASELINE_REV;
  if (baselineRevision) {
    if (!/^[a-f0-9]{40}$/u.test(baselineRevision)) {
      throw new Error("The baseline must be an exact commit SHA");
    }
    const baseline = run("git", [
      "show",
      `${baselineRevision}:tools/emception/toolchain/overlays/libcurl-lite/src/curl_lite.c`,
    ], true).replaceAll("\r\n", "\n");
    const baselineHash = createHash("sha256").update(baseline).digest("hex");
    if (baselineHash === "fb4bdabfc97ce4e38600b032eb6798f1a2abc04c6fcf95c0402a05d1d39c9dd7") {
      const baselineSource = path.join(directory, "baseline-curl.c");
      const baselineExecutable = path.join(directory, process.platform === "win32" ? "baseline.exe" : "baseline");
      writeFileSync(baselineSource, baseline);
      run(process.env.CC ?? (process.platform === "win32" ? "clang" : "gcc"), [
        "-std=c11", "-Wall", "-Wextra", "-Werror", "-fsanitize=address,undefined",
        "-fno-omit-frame-pointer", "-I", path.join(library, "include"),
        "-DCURL_LITE_BASELINE_ONLY",
        `-DCURL_LITE_SOURCE="${baselineSource.replaceAll("\\", "/")}"`,
        path.join(library, "tests", "postfields.c"),
        "-o", baselineExecutable,
      ]);
      const control = spawnSync(baselineExecutable, [], {
        env: { ...environment, ASAN_OPTIONS: "detect_leaks=1:halt_on_error=1" },
        encoding: "utf8",
        timeout: 30000,
      });
      process.stdout.write(control.stdout ?? "");
      process.stderr.write(control.stderr ?? "");
      if (control.error || control.status === 0 ||
          !/AddressSanitizer: heap-buffer-overflow/u.test(control.stderr ?? "")) {
        throw new Error("The reviewed before-source did not reproduce the expected heap-buffer-overflow");
      }
      process.stdout.write(`Before-source control: reproduced heap-buffer-overflow at ${baselineRevision} (${baselineHash})\n`);
    } else {
      process.stdout.write(`Before-source control not applicable: baseline content ${baselineHash} differs from the reviewed vulnerable source\n`);
    }
  }

  for (const name of ["header_callback", "encoding", "postfields"]) {
    const source = path.join(library, "tests", `${name}.c`);
    const common = [
      "-std=c11",
      "-Wall",
      "-Wextra",
      "-Werror",
      "-fsanitize=address,undefined",
      "-I",
      path.join(library, "include"),
      source,
    ];
    const executable = path.join(
      directory,
      process.platform === "win32" ? `${name}.exe` : name,
    );
    run(process.env.CC ?? (process.platform === "win32" ? "clang" : "gcc"), [
      ...common,
      "-fno-omit-frame-pointer",
      "-o",
      executable,
    ]);
    run(executable, []);
    const wasmLoader = path.join(directory, `${name}.cjs`);
    run(emccCommand, [
      ...emccPrefix,
      ...common,
      "-sENVIRONMENT=node",
      "-sALLOW_MEMORY_GROWTH=1",
      "-o",
      wasmLoader,
    ]);
    run(process.execPath, [wasmLoader]);
  }
} finally {
  const resolvedDirectory = realpathSync(directory);
  if (
    path.dirname(resolvedDirectory) !== temporaryBase ||
    !path.basename(resolvedDirectory).startsWith(prefix)
  ) {
    throw new Error(
      "Refusing to remove a regression directory outside its owned temporary path",
    );
  }
  rmSync(resolvedDirectory, { recursive: true, force: true });
}
