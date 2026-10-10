import { spawnSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, realpathSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const library = path.join(root, 'toolchain/overlays/libcurl-lite');
const lock = JSON.parse(readFileSync(path.join(root, 'toolchain/toolchain.lock.json'), 'utf8'));
const expectedVersion = lock.tools.emsdk.version;
const sdk = process.env.EMSDK ?? path.join(root, '.cache/toolchain/emsdk');
const emcc = path.join(sdk, 'upstream/emscripten/emcc');
if (!existsSync(emcc)) {
  throw new Error('The pinned Emscripten SDK must be installed before running curl-lite regressions');
}
const environment = {
  ...process.env,
  EMSDK: sdk,
  EM_CONFIG: process.env.EM_CONFIG ?? path.join(sdk, '.emscripten'),
};

function run(command, args, capture = false) {
  const result = spawnSync(command, args, {
    env: environment,
    encoding: 'utf8',
    stdio: capture ? 'pipe' : 'inherit',
  });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`Command failed (${result.status}): ${command}`);
  return result.stdout;
}

const emccCommand = process.platform === 'win32' ? (process.env.EMSDK_PYTHON ?? 'python') : emcc;
const emccPrefix = process.platform === 'win32' ? [emcc] : [];
const versionOutput = run(emccCommand, [...emccPrefix, '--version'], true);
const actualVersion = /\)\s+([0-9]+\.[0-9]+\.[0-9]+)\b/.exec(versionOutput)?.[1];
if (actualVersion !== expectedVersion) {
  throw new Error(`Emscripten ${actualVersion ?? 'unknown'} does not match pinned ${expectedVersion}`);
}
process.stdout.write(versionOutput);

const temporaryBase = realpathSync(tmpdir());
const prefix = 'emception-curl-regressions-';
const directory = mkdtempSync(path.join(temporaryBase, prefix));
try {
  for (const name of ['header_callback', 'encoding']) {
    const source = path.join(library, 'tests', `${name}.c`);
    const common = ['-std=c11', '-Wall', '-Wextra', '-Werror', '-fsanitize=address,undefined', '-I', path.join(library, 'include'), source];
    const executable = path.join(directory, process.platform === 'win32' ? `${name}.exe` : name);
    run(process.env.CC ?? (process.platform === 'win32' ? 'clang' : 'gcc'), [...common, '-fno-omit-frame-pointer', '-o', executable]);
    run(executable, []);
    const wasmLoader = path.join(directory, `${name}.cjs`);
    run(emccCommand, [...emccPrefix, ...common, '-sENVIRONMENT=node', '-sALLOW_MEMORY_GROWTH=1', '-o', wasmLoader]);
    run(process.execPath, [wasmLoader]);
  }
} finally {
  const resolvedDirectory = realpathSync(directory);
  if (path.dirname(resolvedDirectory) !== temporaryBase || !path.basename(resolvedDirectory).startsWith(prefix)) {
    throw new Error('Refusing to remove a regression directory outside its owned temporary path');
  }
  rmSync(resolvedDirectory, { recursive: true, force: true });
}
