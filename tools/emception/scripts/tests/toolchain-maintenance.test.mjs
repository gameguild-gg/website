import assert from 'node:assert/strict';
import { mkdir, readFile, readdir, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';

async function temporaryRoot(context) {
  const root = await import('node:fs/promises').then(({ mkdtemp }) =>
    mkdtemp(path.join(tmpdir(), 'emception-toolchain-')),
  );
  context.after(async () => {
    const { rm } = await import('node:fs/promises');
    await rm(root, { recursive: true, force: true, maxRetries: 5, retryDelay: 50 });
  });
  await mkdir(path.join(root, 'toolchain'), { recursive: true });
  return root;
}

test('toolchain paths separate tracked policy, disposable cache, and release artifacts', async () => {
  const { toolchainPaths } = await import('../toolchain/paths.ts');
  const root = path.resolve('/workspace/emception');

  assert.deepEqual(toolchainPaths(root), {
    root,
    configFile: path.join(root, 'toolchain', 'toolchain.config.json'),
    lockFile: path.join(root, 'toolchain', 'toolchain.lock.json'),
    overlays: path.join(root, 'toolchain', 'overlays'),
    cache: path.join(root, '.cache', 'toolchain'),
    downloads: path.join(root, '.cache', 'toolchain', 'downloads'),
    sources: path.join(root, '.cache', 'toolchain', 'sources'),
    builds: path.join(root, '.cache', 'toolchain', 'builds'),
    emsdk: path.join(root, '.cache', 'toolchain', 'emsdk'),
    artifacts: path.join(root, 'artifacts', 'toolchain'),
    tools: path.join(root, 'artifacts', 'toolchain', 'tools'),
    sysroot: path.join(root, 'artifacts', 'toolchain', 'sysroot'),
    stagedSysroot: path.join(root, 'artifacts', 'toolchain', 'stage', 'sysroot'),
    receipts: path.join(root, 'artifacts', 'toolchain', 'receipts'),
    releaseCdn: path.join(root, 'artifacts', 'toolchain', 'release', 'cdn'),
    manifestFile: path.join(root, 'artifacts', 'toolchain', 'release', 'cdn', 'manifest.json'),
    publicCdn: path.join(root, 'public', 'cdn'),
    packageCdn: path.join(root, 'packages', 'toolchain', 'cdn'),
    compatibilityCdn: path.join(root, 'packages', 'core', 'cdn'),
  });
});

test('Toolchain processes use portable pnpm launchers and avoid remote tar parsing', async () => {
  const { describePnpmFailure, pnpmInvocation } = await import('../toolchain/cli.ts');
  const { normalizeWindowsCpythonMakefile } = await import('../lib/cpython-windows.ts');
  const { rewriteMsysPathReferences, toMsysPath } = await import('../lib/posix-path.ts');
  const { TOOLCHAIN_RECIPES } = await import('../toolchain/recipes.ts');

  assert.deepEqual(pnpmInvocation('win32', 'C:\\Windows\\System32\\cmd.exe'), {
    executable: 'C:\\Windows\\System32\\cmd.exe',
    arguments: ['/d', '/s', '/c', 'pnpm'],
  });
  assert.deepEqual(pnpmInvocation('linux'), { executable: 'pnpm', arguments: [] });
  assert.deepEqual(pnpmInvocation('darwin', '/bin/sh', false), {
    executable: 'corepack',
    arguments: ['pnpm'],
  });
  assert.equal(toMsysPath('E:\\sources\\cpython\\configure', 'win32'), '/e/sources/cpython/configure');
  assert.equal(toMsysPath('/sources/cpython/configure', 'linux'), '/sources/cpython/configure');
  assert.equal(
    rewriteMsysPathReferences('srcdir=/e/sources/cpython\n', 'E:\\sources\\cpython', 'win32'),
    'srcdir=E:/sources/cpython\n',
  );
  assert.equal(
    normalizeWindowsCpythonMakefile(
      'srcdir=/e/sources/cpython\nSOABI=\t\tcpython-313\nMULTIARCH=\t\t\nMULTIARCH_CPPFLAGS = \nLIBS=\t\t-ldl -lpthread -latomic\n',
      'E:\\sources\\cpython',
    ),
    'srcdir=E:/sources/cpython\nSOABI=\t\tcpython-313-wasm32-emscripten\n'
      + 'MULTIARCH=\t\twasm32-emscripten\nMULTIARCH_CPPFLAGS = -DMULTIARCH=\\"wasm32-emscripten\\"\n'
      + 'LIBS=\t\t-ldl -lpthread\n',
  );
  assert.match(
    describePnpmFailure('recipe:emsdk', {
      status: null,
      signal: null,
      error: Object.assign(new Error('spawnSync pnpm.cmd EINVAL'), { code: 'EINVAL' }),
    }),
    /recipe:emsdk.*EINVAL/,
  );
  assert.deepEqual(
    TOOLCHAIN_RECIPES.emsdk.outputs,
    ['.cache/toolchain/emsdk/upstream/emscripten/emcc.py'],
  );
});

test('Binaryen optimization inherits the bounded Toolchain concurrency', async () => {
  const { ensureBinaryenConcurrency } = await import('../lib/emsdk.ts');
  const inherited = {};
  const explicit = { BINARYEN_CORES: '2' };

  ensureBinaryenConcurrency(inherited, 4);
  ensureBinaryenConcurrency(explicit, 4);

  assert.equal(inherited.BINARYEN_CORES, '4');
  assert.equal(explicit.BINARYEN_CORES, '2');
  assert.throws(() => ensureBinaryenConcurrency({}, 0), /positive integer/);
});

test('LLVM driver objects use the configured Makefile from their generated subdirectory', async (context) => {
  const { createCMakeObjectBuildPlan } = await import('../lib/cmake-object-targets.ts');
  const root = await temporaryRoot(context);
  const buildDirectory = path.join(root, 'build');
  const targetSubdirectory = path.join(buildDirectory, 'tools', 'clang', 'tools', 'driver');
  await mkdir(targetSubdirectory, { recursive: true });
  await writeFile(
    path.join(buildDirectory, 'CMakeCache.txt'),
    'CMAKE_GENERATOR:INTERNAL=Unix Makefiles\nCMAKE_MAKE_PROGRAM:FILEPATH=/usr/bin/gmake\n',
  );

  assert.deepEqual(createCMakeObjectBuildPlan({
    buildDirectory,
    concurrency: 4,
    objectDirectory: 'CMakeFiles/clang.dir',
    objectFiles: ['driver.cpp.o', 'clang-driver.cpp.o'],
    targetSubdirectory,
  }), {
    executable: '/usr/bin/gmake',
    arguments: ['-C', targetSubdirectory, '-j', '4', 'driver.cpp.o', 'clang-driver.cpp.o'],
    workingDirectory: buildDirectory,
  });
});

test('LLVM driver objects retain root targets for non-Makefile CMake generators', async (context) => {
  const { createCMakeObjectBuildPlan } = await import('../lib/cmake-object-targets.ts');
  const root = await temporaryRoot(context);
  const buildDirectory = path.join(root, 'build');
  const targetSubdirectory = path.join(buildDirectory, 'tools', 'lld', 'tools', 'lld');
  await mkdir(targetSubdirectory, { recursive: true });
  await writeFile(path.join(buildDirectory, 'CMakeCache.txt'), 'CMAKE_GENERATOR:INTERNAL=Ninja\n');

  assert.deepEqual(createCMakeObjectBuildPlan({
    buildDirectory,
    concurrency: 2,
    objectDirectory: 'CMakeFiles/lld.dir',
    objectFiles: ['lld.cpp.o', 'lld-driver.cpp.o'],
    targetSubdirectory,
  }), {
    executable: 'cmake',
    arguments: [
      '--build',
      buildDirectory,
      '--parallel',
      '2',
      '--target',
      'tools/lld/tools/lld/CMakeFiles/lld.dir/lld.cpp.o',
      'tools/lld/tools/lld/CMakeFiles/lld.dir/lld-driver.cpp.o',
    ],
    workingDirectory: buildDirectory,
  });
});

test('Emscripten sysroot copies exclude host Python bytecode caches', async (context) => {
  const { copyRuntimeDirectoryContents, copyRuntimeSourceTree } = await import('../lib/runtime-source-tree.ts');
  const root = await temporaryRoot(context);
  const source = path.join(root, 'tools');
  const destination = path.join(root, 'sysroot', 'tools');

  await mkdir(path.join(source, '__pycache__'), { recursive: true });
  await mkdir(path.join(destination, '__pycache__'), { recursive: true });
  await writeFile(path.join(source, 'building.py'), 'runtime source');
  await writeFile(path.join(source, '__pycache__', 'building.cpython-314.pyc'), 'host cache');
  await writeFile(path.join(source, 'settings.pyo'), 'host cache');
  await writeFile(path.join(destination, '__pycache__', 'stale.cpython-314.pyc'), 'stale host cache');

  copyRuntimeSourceTree(source, destination);

  assert.equal(await readFile(path.join(destination, 'building.py'), 'utf8'), 'runtime source');
  await assert.rejects(readFile(path.join(destination, '__pycache__', 'building.cpython-314.pyc')));
  await assert.rejects(readFile(path.join(destination, '__pycache__', 'stale.cpython-314.pyc')));
  await assert.rejects(readFile(path.join(destination, 'settings.pyo')));

  const contentsDestination = path.join(root, 'include');
  copyRuntimeDirectoryContents(source, contentsDestination);
  assert.equal(await readFile(path.join(contentsDestination, 'building.py'), 'utf8'), 'runtime source');
  await assert.rejects(readFile(path.join(contentsDestination, '__pycache__', 'building.cpython-314.pyc')));
});

test('sysroot cache revision does not change its executable pnpm script', async () => {
  const { TOOLCHAIN_RECIPES } = await import('../toolchain/recipes.ts');
  const scripts = [];

  await TOOLCHAIN_RECIPES.sysroot.run({
    root: '/workspace/emception',
    force: false,
    runScript(script) {
      scripts.push(script);
    },
  });

  assert.deepEqual(scripts, ['recipe:sysroot']);
  assert.equal(TOOLCHAIN_RECIPES.sysroot.cacheKey, 'sysroot:no-host-bytecode-v2');
});

test('toolchain lock serialization is stable and validates the CMake major policy', async (context) => {
  const root = await temporaryRoot(context);
  const { calculateConfigHash, loadToolchainState, serializeToolchainLock } = await import('../toolchain/lock.ts');
  const config = {
    schemaVersion: 1,
    runtimeAbi: 'emception-browser-v1',
    constraints: { cmake: '<4' },
    emsdkGroup: [],
  };
  const configHash = calculateConfigHash(config);
  const lock = {
    schemaVersion: 1,
    configHash,
    tools: {
      emsdk: {
        version: '5.0.7',
        source: {
          kind: 'git-archive',
          repository: 'emscripten-core/emsdk',
          commit: '0123456789abcdef0123456789abcdef01234567',
          url: 'https://codeload.github.com/emscripten-core/emsdk/tar.gz/0123456789abcdef0123456789abcdef01234567',
          sha256: 'a'.repeat(64),
        },
      },
      cmake: {
        version: '3.31.12',
        source: {
          kind: 'archive',
          url: 'https://example.invalid/cmake.tar.gz',
          sha256: 'b'.repeat(64),
        },
      },
    },
  };

  const first = serializeToolchainLock(lock);
  const second = serializeToolchainLock({ ...lock, tools: { cmake: lock.tools.cmake, emsdk: lock.tools.emsdk } });
  assert.equal(first, second);
  assert.equal(first.includes('generatedAt'), false);

  await writeFile(path.join(root, 'toolchain', 'toolchain.config.json'), `${JSON.stringify(config, null, 2)}\n`);
  await writeFile(path.join(root, 'toolchain', 'toolchain.lock.json'), first);
  const state = await loadToolchainState(root);
  assert.equal(state.lock.configHash, configHash);
  assert.equal(state.lock.tools.cmake.version, '3.31.12');

  const invalid = JSON.parse(first);
  invalid.tools.cmake.version = '4.0.0';
  await writeFile(path.join(root, 'toolchain', 'toolchain.lock.json'), `${JSON.stringify(invalid, null, 2)}\n`);
  await assert.rejects(loadToolchainState(root), /CMake 4\.0\.0 violates configured constraint <4/);
});

test('checked-in toolchain policy and lock agree without dynamic timestamps', async () => {
  const { loadToolchainState } = await import('../toolchain/lock.ts');
  const { hashDirectory } = await import('../toolchain/sources.ts');
  const root = path.resolve(import.meta.dirname, '..', '..');
  const state = await loadToolchainState(root);
  const source = await readFile(path.join(root, 'toolchain', 'toolchain.lock.json'), 'utf8');

  assert.equal(state.config.runtimeAbi, 'emception-browser-v1');
  assert.equal(state.lock.tools.emsdk.version, '5.0.7');
  assert.equal(state.lock.tools.cmake.version, '3.31.12');
  assert.equal(source.includes('generatedAt'), false);
  assert.equal(
    hashDirectory(path.join(root, state.lock.tools.curlLite.source.path)),
    state.lock.tools.curlLite.source.contentHash,
  );
});

test('updating EMSDK replaces the complete derived component group without mutating the input lock', async () => {
  const { planToolchainUpdate } = await import('../toolchain/sources.ts');
  const config = {
    schemaVersion: 1,
    runtimeAbi: 'emception-browser-v1',
    constraints: { cmake: '<4' },
    emsdkGroup: ['llvm', 'binaryen', 'python', 'sdl3'],
  };
  const source = (name, version, derivedFrom) => ({
    version,
    ...(derivedFrom ? { derivedFrom } : {}),
    source: {
      kind: 'emsdk-component',
      emsdkVersion: version,
      revision: `${name}-${version}`,
      contentHash: name[0].repeat(64),
    },
  });
  const original = {
    schemaVersion: 1,
    configHash: 'c'.repeat(64),
    tools: {
      emsdk: source('emsdk', '5.0.7'),
      llvm: source('llvm', '23.1.0', 'emsdk'),
      binaryen: source('binaryen', '129', 'emsdk'),
      python: source('python', '3.13.3', 'emsdk'),
      sdl3: source('sdl3', 'emsdk-5.0.7', 'emsdk'),
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/cmake', sha256: 'd'.repeat(64) },
      },
    },
  };
  const before = JSON.stringify(original);
  const provider = {
    async resolve(name, requested) {
      assert.equal(name, 'emsdk');
      assert.equal(requested, 'latest');
      return source('emsdk', '6.0.5');
    },
    async inspectEmsdk() {
      return {
        llvm: source('llvm', '24.0.0', 'emsdk'),
        binaryen: source('binaryen', '130', 'emsdk'),
        python: source('python', '3.14.0', 'emsdk'),
        sdl3: source('sdl3', 'emsdk-6.0.5', 'emsdk'),
      };
    },
    async latestVersion() {
      return '6.0.5';
    },
  };

  const updated = await planToolchainUpdate(config, original, 'emsdk', 'latest', provider);

  assert.equal(JSON.stringify(original), before);
  assert.equal(updated.tools.emsdk.version, '6.0.5');
  assert.equal(updated.tools.llvm.version, '24.0.0');
  assert.equal(updated.tools.binaryen.version, '130');
  assert.equal(updated.tools.python.version, '3.14.0');
  assert.equal(updated.tools.sdl3.version, 'emsdk-6.0.5');
  assert.equal(updated.tools.cmake.version, '3.31.12');
});

test('outdated is read-only and derived tools must be updated through EMSDK', async () => {
  const { findOutdatedTools, planToolchainUpdate } = await import('../toolchain/sources.ts');
  const config = {
    schemaVersion: 1,
    runtimeAbi: 'emception-browser-v1',
    constraints: { cmake: '<4' },
    emsdkGroup: ['llvm'],
  };
  const lock = {
    schemaVersion: 1,
    configHash: 'e'.repeat(64),
    tools: {
      emsdk: {
        version: '5.0.7',
        source: { kind: 'archive', url: 'https://example.invalid/emsdk', sha256: 'a'.repeat(64) },
      },
      llvm: {
        version: '23.1.0',
        derivedFrom: 'emsdk',
        source: { kind: 'archive', url: 'https://example.invalid/llvm', sha256: 'b'.repeat(64) },
      },
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/cmake', sha256: 'c'.repeat(64) },
      },
    },
  };
  const before = JSON.stringify(lock);
  const provider = {
    async latestVersion(name) {
      return name === 'cmake' ? '3.31.13' : '5.0.7';
    },
    async resolve() {
      throw new Error('not used');
    },
    async inspectEmsdk() {
      throw new Error('not used');
    },
  };

  assert.deepEqual(await findOutdatedTools(config, lock, provider), [
    { name: 'cmake', current: '3.31.12', latest: '3.31.13' },
  ]);
  assert.equal(JSON.stringify(lock), before);
  await assert.rejects(planToolchainUpdate(config, lock, 'llvm', '24.0.0', provider), /controlled by emsdk/);
});

test('clean scopes remove only generated toolchain state', async (context) => {
  const { cleanToolchain } = await import('../toolchain/clean.ts');
  const root = await temporaryRoot(context);
  const cacheMarker = path.join(root, '.cache', 'toolchain', 'sources', 'source.txt');
  const artifactMarker = path.join(root, 'artifacts', 'toolchain', 'tools', 'clang.wasm');
  const dependencyMarker = path.join(root, 'node_modules', 'keep.txt');
  const overlayMarker = path.join(root, 'toolchain', 'overlays', 'cpython', 'patches', 'keep.patch');
  for (const marker of [cacheMarker, artifactMarker, dependencyMarker, overlayMarker]) {
    await mkdir(path.dirname(marker), { recursive: true });
    await writeFile(marker, 'keep');
  }
  const exists = async (filename) => readFile(filename, 'utf8').then(() => true, () => false);

  await cleanToolchain(root, 'artifacts');
  assert.equal(await exists(artifactMarker), false);
  assert.equal(await exists(cacheMarker), true);
  assert.equal(await exists(dependencyMarker), true);
  assert.equal(await exists(overlayMarker), true);

  await mkdir(path.dirname(artifactMarker), { recursive: true });
  await writeFile(artifactMarker, 'again');
  await cleanToolchain(root, 'cache');
  assert.equal(await exists(cacheMarker), false);
  assert.equal(await exists(artifactMarker), true);

  await cleanToolchain(root, 'all');
  assert.equal(await exists(artifactMarker), false);
  assert.equal(await exists(dependencyMarker), true);
  assert.equal(await exists(overlayMarker), true);
});

test('build scripts select versions only from the toolchain lock', async () => {
  const scriptsRoot = path.resolve(import.meta.dirname, '..');
  const entries = await readdir(scriptsRoot, { withFileTypes: true });
  const buildScripts = entries
    .filter((entry) => entry.isFile() && /^(build-|deploy-cpython|populate-sysroot|setup-emsdk|generate-manifest).*\.(?:ts|mjs)$/.test(entry.name))
    .map((entry) => path.join(scriptsRoot, entry.name));
  const toolVersionOverride = /process\.env\.(?:EMSDK|LLVM|BINARYEN|PYTHON|CMAKE|BROTLI|IMGUI|RAYLIB|RAYGUI|PHYSAC|ALLEGRO|CURL_LITE)_VERSION/;

  for (const filename of buildScripts) {
    const source = await readFile(filename, 'utf8');
    assert.doesNotMatch(source, /pinned-versions/, `${path.basename(filename)} still imports pinned versions`);
    assert.doesNotMatch(source, toolVersionOverride, `${path.basename(filename)} accepts a version override`);
    assert.doesNotMatch(source, /api\.github\.com\/.*releases|Detect(?:ing)? latest/i, `${path.basename(filename)} resolves latest during a build`);
    assert.doesNotMatch(source, /git clone|refs\/(?:tags|heads)|curl[^\n]*https?:\/\//, `${path.basename(filename)} bypasses the locked source manager`);
  }
});

test('legacy Toolchain build commands are thin aliases to the maintenance CLI', async () => {
  const root = path.resolve(import.meta.dirname, '..', '..');
  const packageJson = JSON.parse(await readFile(path.join(root, 'package.json'), 'utf8'));
  const aliases = [
    'build:emsdk', 'build:binaryen', 'build:cpython', 'build:llvm', 'build:libcurl-lite',
    'build:cmake', 'build:sdl3', 'build:imgui', 'build:raylib', 'build:allegro',
    'build:sysroot', 'build:stage:sysroot', 'build:manifest', 'build:brotli', 'build:bundles',
    'build:toolchain:light', 'build:toolchain:heavy', 'build:toolchain:parallel',
    'build:graphics:parallel', 'build:graphics', 'build:cdn:serial', 'build:cdn', 'build:pipeline',
  ];

  for (const alias of aliases) {
    assert.match(packageJson.scripts[alias], /^(?:pnpm )?toolchain (?:build|release)/, `${alias} bypasses the CLI`);
  }
  const recipes = await readFile(path.join(root, 'scripts', 'toolchain', 'recipes.ts'), 'utf8');
  assert.doesNotMatch(recipes, /scriptRecipe\([^)]*'build:/s);
});

test('locked source checksum is verified before extraction and workspace hashes are deterministic', async (context) => {
  const { createHash } = await import('node:crypto');
  const { ensureLockedSource, hashDirectory } = await import('../toolchain/sources.ts');
  const root = await temporaryRoot(context);
  const overlay = path.join(root, 'toolchain', 'overlays', 'local');
  await mkdir(path.join(overlay, 'nested'), { recursive: true });
  await writeFile(path.join(overlay, 'nested', 'b.txt'), 'second');
  await writeFile(path.join(overlay, 'a.txt'), 'first');
  const firstHash = hashDirectory(overlay);
  const secondHash = hashDirectory(overlay);
  assert.equal(firstHash, secondHash);
  assert.match(firstHash, /^[a-f0-9]{64}$/);

  const poisoned = Buffer.from('not the locked archive');
  const expectedHash = createHash('sha256').update('expected archive').digest('hex');
  const download = path.join(root, '.cache', 'toolchain', 'downloads', `cmake-${expectedHash}.archive`);
  await mkdir(path.dirname(download), { recursive: true });
  await writeFile(download, poisoned);
  const lock = {
    schemaVersion: 1,
    configHash: 'a'.repeat(64),
    tools: {
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/cmake.tar.gz', sha256: expectedHash },
      },
    },
  };
  const destination = path.join(root, '.cache', 'toolchain', 'sources', 'cmake');

  assert.throws(
    () => ensureLockedSource(root, lock, 'cmake', destination, 'CMakeLists.txt'),
    /Checksum mismatch/,
  );
  assert.equal(await readFile(download, 'utf8'), poisoned.toString());
  await assert.rejects(readFile(path.join(destination, 'CMakeLists.txt')));
});

test('workspace source hashes ignore checkout line-ending representation', async (context) => {
  const { hashDirectory } = await import('../toolchain/sources.ts');
  const root = await temporaryRoot(context);
  const lf = path.join(root, 'lf');
  const crlf = path.join(root, 'crlf');
  await mkdir(lf, { recursive: true });
  await mkdir(crlf, { recursive: true });
  await writeFile(path.join(lf, 'overlay.c'), 'int main(void) {\n  return 0;\n}\n');
  await writeFile(path.join(crlf, 'overlay.c'), 'int main(void) {\r\n  return 0;\r\n}\r\n');

  assert.equal(hashDirectory(lf), hashDirectory(crlf));
});

async function lockedGitExportFixture(context, useReviewedHash) {
  const { createHash } = await import('node:crypto');
  const { gzipSync } = await import('node:zlib');
  const { createDeterministicTar } = await import('../lib/deterministic-tar.ts');
  const root = await temporaryRoot(context);
  const commit = '0123456789abcdef0123456789abcdef01234567';
  const exports = (abbreviation) => gzipSync(createDeterministicTar([
    { path: `/llvm-project-${commit}/CMakeLists.txt`, data: new TextEncoder().encode('project(unchanged)\n') },
    { path: `/llvm-project-${commit}/.git_archival.txt`, data: new TextEncoder().encode(`node: ${commit}\ndescribe-name: v1-1-g${abbreviation}\n`) },
  ]));
  const original = exports(commit.slice(0, 7));
  const reviewed = exports(commit.slice(0, 9));
  const oldHash = createHash('sha256').update(original).digest('hex');
  const reviewedHash = createHash('sha256').update(reviewed).digest('hex');
  assert.notEqual(oldHash, reviewedHash);
  const expectedHash = useReviewedHash ? reviewedHash : oldHash;
  const lock = {
    schemaVersion: 1,
    configHash: 'a'.repeat(64),
    tools: {
      llvm: {
        version: '23.0.0git',
        source: {
          kind: 'git-archive', repository: 'llvm/llvm-project', commit,
          url: `https://example.invalid/llvm-project/${commit}.tar.gz`, sha256: expectedHash,
        },
      },
    },
  };
  const download = path.join(root, '.cache', 'toolchain', 'downloads', `llvm-${expectedHash}.archive`);
  await mkdir(path.dirname(download), { recursive: true });
  await writeFile(download, reviewed);
  return { root, lock, commit, expectedHash, oldHash, reviewedHash, destination: path.join(root, '.cache', 'toolchain', 'sources', 'llvm') };
}

test('a fixed Git commit does not waive a changed archive checksum', async (context) => {
  const { ensureLockedSource } = await import('../toolchain/sources.ts');
  const { root, lock, destination } = await lockedGitExportFixture(context, false);
  const before = JSON.stringify(lock);

  assert.throws(() => ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), /Checksum mismatch/);
  await assert.rejects(readFile(path.join(destination, 'CMakeLists.txt')));
  await assert.rejects(readFile(path.join(destination, '.emception-source.json')));
  assert.equal(JSON.stringify(lock), before);
});

test('a reviewed archive checksum materializes the fixed commit and records its identity', async (context) => {
  const { ensureLockedSource } = await import('../toolchain/sources.ts');
  const { root, lock, commit, expectedHash, destination } = await lockedGitExportFixture(context, true);

  assert.equal(ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), destination);
  assert.equal(await readFile(path.join(destination, 'CMakeLists.txt'), 'utf8'), 'project(unchanged)\n');
  assert.equal(await readFile(path.join(destination, '.git_archival.txt'), 'utf8'),
    `node: ${commit}\ndescribe-name: v1-1-g${commit.slice(0, 9)}\n`);
  assert.deepEqual(JSON.parse(await readFile(path.join(destination, '.emception-source.json'), 'utf8')),
    { name: 'llvm', version: '23.0.0git', sha256: expectedHash });
  assert.equal(lock.tools.llvm.source.commit, commit);
});

test('reviewed Git archive variants retain the downloaded hash in the source receipt', async (context) => {
  const { ensureLockedSource } = await import('../toolchain/sources.ts');
  const { root, lock, reviewedHash, destination } = await lockedGitExportFixture(context, false);
  lock.tools.llvm.source.reviewedArchiveVariants = [{ sha256: reviewedHash, gitTree: '1'.repeat(40), review: 'Compared all entries to the pinned Git tree.' }];
  const before = JSON.stringify(lock);
  assert.equal(ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), destination);
  assert.deepEqual(JSON.parse(await readFile(path.join(destination, '.emception-source.json'), 'utf8')),
    { name: 'llvm', version: '23.0.0git', sha256: reviewedHash });
  assert.equal(ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), destination);
  assert.equal(JSON.stringify(lock), before);
  delete lock.tools.llvm.source.reviewedArchiveVariants;
  assert.throws(() => ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), /Checksum mismatch/);
});

test('Git archive variant review rejects malformed, duplicate and unsupported identities', async () => {
  const { calculateConfigHash, validateToolchainState } = await import('../toolchain/lock.ts');
  const config = { schemaVersion: 1, runtimeAbi: 'test', constraints: { cmake: '<4' }, emsdkGroup: [] };
  const source = { kind: 'git-archive', repository: 'llvm/llvm-project', commit: '1'.repeat(40),
    url: 'https://example.invalid/llvm', sha256: 'a'.repeat(64) };
  const variant = { sha256: 'b'.repeat(64), gitTree: '2'.repeat(40), review: 'Complete archive comparison.' };
  const makeLock = (variants) => ({ schemaVersion: 1, configHash: calculateConfigHash(config), tools: {
    cmake: { version: '3.31.12', source: { kind: 'archive', url: 'https://example.invalid/cmake', sha256: 'c'.repeat(64) } },
    llvm: { version: '23', source: { ...source, reviewedArchiveVariants: variants } },
  } });
  validateToolchainState(config, makeLock([variant]));
  for (const variants of [[], null, 'unreviewed', [{ ...variant, sha256: 'bad' }], [{ ...variant, sha256: source.sha256 }],
    [variant, variant], [{ ...variant, gitTree: 'bad' }], [{ ...variant, review: '' }], [null]]) {
    assert.throws(() => validateToolchainState(config, makeLock(variants)), /reviewedArchiveVariants/);
  }
  const unsupported = makeLock([variant]);
  unsupported.tools.cmake.source.reviewedArchiveVariants = [variant];
  assert.throws(() => validateToolchainState(config, unsupported), /reviewedArchiveVariants.*git-archive/);
});

test('reviewed Git archive variants still reject every unlisted archive before extraction', async (context) => {
  const { ensureLockedSource } = await import('../toolchain/sources.ts');
  const { root, lock, expectedHash, destination } = await lockedGitExportFixture(context, false);
  lock.tools.llvm.source.reviewedArchiveVariants = [{ sha256: 'f'.repeat(64), gitTree: '1'.repeat(40), review: 'A different reviewed variant.' }];
  assert.throws(() => ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), /Checksum mismatch/);
  await assert.rejects(readFile(path.join(destination, '.emception-source.json')));
  assert.equal(lock.tools.llvm.source.sha256, expectedHash);
});

test('the committed LLVM CI archive variant is bound to the fully reviewed immutable tree', async () => {
  const { lockedArchiveHashes } = await import('../toolchain/lock.ts');
  const lock = JSON.parse(await readFile(new URL('../../toolchain/toolchain.lock.json', import.meta.url), 'utf8'));
  const source = lock.tools.llvm.source;
  const digest = '96cf1ae663d1ede1f76ea2ef9bada1c942b8fdfc861792e972fabf5c554e17ce';
  const proof = JSON.parse(await readFile(new URL('../../toolchain/reviews/llvm-7b58716d96c3ae4c0c4e6f72e29b16137bb6224b-96cf1ae6.json', import.meta.url), 'utf8'));
  assert.equal(source.commit, '7b58716d96c3ae4c0c4e6f72e29b16137bb6224b');
  assert.equal(source.url, `https://codeload.github.com/llvm/llvm-project/tar.gz/${source.commit}`);
  assert.equal(source.sha256, '0d7fb3e45c6d2916d4ad4613fc3ee099076d088c13e5ca4b9562bab83319a4a5');
  assert.equal(proof.archiveSha256, digest);
  assert.equal(proof.commit, source.commit);
  assert.equal(proof.normalizedArchiveGitTree, proof.upstreamGitTree);
  assert.equal(proof.upstreamGitTree, '34cd378b6fba47d5bd42003cdaf9505179879004');
  assert.equal(proof.verifiedFileAndSymlinkEntries, 175014);
  assert.equal(proof.allPathsModesBlobsMatchPinnedTreeAfterReviewedExportTransforms, true);
  assert.equal(proof.unverifiedDifferences, 0);
  assert.equal(proof.extraMissingOrDuplicateFiles, false);
  assert.equal(proof.onlyContentDifference, 'clang/bindings/python/.git_archival.txt');
  assert.equal(proof.describeName, `llvmorg-23-init-13463-g${source.commit.slice(0, 14)}`);
  assert(lockedArchiveHashes(source).includes(digest));
  const variant = source.reviewedArchiveVariants.find(entry => entry.sha256 === digest);
  assert.equal(variant.gitTree, proof.upstreamGitTree);
  assert(variant.review.includes('96cf1ae6.json'));
});

test('release lock provenance retains reviewed variants without mutating the lock', async (context) => {
  const { lockedSourceProvenance } = await import('../toolchain/lock.ts');
  const { lock, oldHash, reviewedHash, commit } = await lockedGitExportFixture(context, false);
  const variant = { sha256: reviewedHash, gitTree: '1'.repeat(40), review: 'Complete archive comparison.' };
  lock.tools.llvm.source.reviewedArchiveVariants = [variant];
  const provenance = lockedSourceProvenance(lock);
  assert.deepEqual(provenance.llvm, { version: '23.0.0git', revision: commit, sha256: oldHash, reviewedArchiveVariants: [variant] });
  provenance.llvm.reviewedArchiveVariants[0].review = 'changed consumer copy';
  assert.equal(lock.tools.llvm.source.reviewedArchiveVariants[0].review, variant.review);
});

test('committed LLVM archive identities have matching immutable review evidence', async () => {
  const { lockedArchiveHashes } = await import('../toolchain/lock.ts');
  const lock = JSON.parse(await readFile(new URL('../../toolchain/toolchain.lock.json', import.meta.url), 'utf8'));
  const source = lock.tools.llvm.source;
  const reviewPath = `../../toolchain/reviews/llvm-${source.commit}.json`;
  const review = JSON.parse(await readFile(new URL(reviewPath, import.meta.url), 'utf8'));

  assert.equal(source.repository, review.repository);
  assert.equal(source.commit, review.commit);
  assert.deepEqual([...lockedArchiveHashes(source)].sort(), review.archives.map((archive) => archive.sha256).sort());
  for (const variant of source.reviewedArchiveVariants) {
    assert.equal(variant.gitTree, review.upstreamGitTree);
  }
  for (const additional of review.additionalArchiveReviews) {
    assert.ok(source.reviewedArchiveVariants.some((variant) => variant.sha256 === additional.sha256));
    assert.equal(additional.upstreamGitTree, review.upstreamGitTree);
    assert.equal(additional.verifiedFiles, review.verifiedFiles);
    assert.equal(additional.reconstructedTreeMatchesPinnedCommit, true);
    assert.equal(additional.originalExportTransformsVerifiedAgainstUpstreamGitBlobs, true);
    assert.equal(additional.archiveDifferences.length, 1);
    assert.equal(additional.archiveDifferences[0].path, 'clang/bindings/python/.git_archival.txt');
    assert.equal(additional.headerDifferences.length, 1);
    assert.match(additional.verificationEvidenceSHA256, /^[0-9a-f]{64}$/);
    assert.match(additional.headerComparisonEvidenceSHA256, /^[0-9a-f]{64}$/);
  }
});

test('multiple reviewed Git variants retain the exact downloaded identity', async (context) => {
  const { ensureLockedSource } = await import('../toolchain/sources.ts');
  const { root, lock, reviewedHash, destination } = await lockedGitExportFixture(context, false);
  lock.tools.llvm.source.reviewedArchiveVariants = [
    { sha256: 'f'.repeat(64), gitTree: '1'.repeat(40), review: 'A separate independently reviewed variant.' },
    { sha256: reviewedHash, gitTree: '1'.repeat(40), review: 'All archive entries compared to the pinned Git tree.' },
  ];
  const before = JSON.stringify(lock);
  assert.equal(ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), destination);
  assert.equal(JSON.parse(await readFile(path.join(destination, '.emception-source.json'), 'utf8')).sha256, reviewedHash);
  assert.equal(JSON.stringify(lock), before);
  lock.tools.llvm.source.reviewedArchiveVariants.pop();
  assert.throws(() => ensureLockedSource(root, lock, 'llvm', destination, 'CMakeLists.txt'), /Checksum mismatch/);
});

test('locked archives extract through the cross-platform Node implementation', async (context) => {
  const { gzipSync } = await import('node:zlib');
  const { createDeterministicTar } = await import('../lib/deterministic-tar.ts');
  const { extractArchive } = await import('../toolchain/sources.ts');
  const root = await temporaryRoot(context);
  const archive = path.join(root, 'source.tar.gz');
  const destination = path.join(root, 'extracted');
  const tar = createDeterministicTar([
    { path: '/source-root/CMakeLists.txt', data: new TextEncoder().encode('project(portable)\n') },
  ]);
  await writeFile(archive, gzipSync(tar));

  extractArchive(archive, destination, 1);

  assert.equal(await readFile(path.join(destination, 'CMakeLists.txt'), 'utf8'), 'project(portable)\n');
});

test('toolchain update dry-run is read-only and accepted updates replace the lock atomically', async (context) => {
  const { calculateConfigHash, serializeToolchainLock } = await import('../toolchain/lock.ts');
  const { runToolchainCli } = await import('../toolchain/cli.ts');
  const root = await temporaryRoot(context);
  const config = {
    schemaVersion: 1,
    runtimeAbi: 'emception-browser-v1',
    constraints: { cmake: '<4' },
    emsdkGroup: [],
  };
  const lock = {
    schemaVersion: 1,
    configHash: calculateConfigHash(config),
    tools: {
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/old', sha256: 'a'.repeat(64) },
      },
    },
  };
  await writeFile(path.join(root, 'toolchain', 'toolchain.config.json'), `${JSON.stringify(config, null, 2)}\n`);
  await writeFile(path.join(root, 'toolchain', 'toolchain.lock.json'), serializeToolchainLock(lock));
  const provider = {
    async latestVersion() { return '3.31.13'; },
    async resolve(name, requested) {
      assert.equal(name, 'cmake');
      assert.equal(requested, 'latest');
      return {
        version: '3.31.13',
        source: { kind: 'archive', url: 'https://example.invalid/new', sha256: 'b'.repeat(64) },
      };
    },
    async inspectEmsdk() { throw new Error('not used'); },
  };
  const output = [];

  await runToolchainCli(['update', 'cmake', 'latest', '--dry-run'], { root, provider, output: (line) => output.push(line) });
  assert.equal((JSON.parse(await readFile(path.join(root, 'toolchain', 'toolchain.lock.json'), 'utf8'))).tools.cmake.version, '3.31.12');

  await runToolchainCli(['update', 'cmake', 'latest'], { root, provider, output: (line) => output.push(line) });
  assert.equal((JSON.parse(await readFile(path.join(root, 'toolchain', 'toolchain.lock.json'), 'utf8'))).tools.cmake.version, '3.31.13');
  const toolchainEntries = await readdir(path.join(root, 'toolchain'));
  assert.deepEqual(toolchainEntries.sort(), ['toolchain.config.json', 'toolchain.lock.json']);
  assert.equal(output.some((line) => line.includes('dry-run')), true);
});

test('build receipts reject changed outputs, dependencies, recipes, and overlays', async (context) => {
  const { calculateConfigHash, serializeToolchainLock } = await import('../toolchain/lock.ts');
  const { executeBuildRecipe } = await import('../toolchain/receipts.ts');
  const root = await temporaryRoot(context);
  const config = { schemaVersion: 1, runtimeAbi: 'abi', constraints: { cmake: '<4' }, emsdkGroup: [] };
  const lock = {
    schemaVersion: 1,
    configHash: calculateConfigHash(config),
    tools: {
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/cmake', sha256: 'c'.repeat(64) },
      },
    },
  };
  await writeFile(path.join(root, 'toolchain', 'toolchain.config.json'), `${JSON.stringify(config, null, 2)}\n`);
  await writeFile(path.join(root, 'toolchain', 'toolchain.lock.json'), serializeToolchainLock(lock));
  const overlay = path.join(root, 'toolchain', 'overlays', 'test', 'patch.txt');
  await mkdir(path.dirname(overlay), { recursive: true });
  await writeFile(overlay, 'patch-v1');
  const depOutput = path.join('artifacts', 'toolchain', 'tools', 'dependency.wasm');
  const childOutput = path.join('artifacts', 'toolchain', 'tools', 'child.wasm');
  let dependencyRuns = 0;
  let childRuns = 0;
  const recipes = {
    dependency: {
      name: 'dependency', dependencies: [], lockEntries: ['cmake'], outputs: [depOutput],
      async run({ root: recipeRoot }) {
        dependencyRuns += 1;
        const output = path.join(recipeRoot, depOutput);
        await mkdir(path.dirname(output), { recursive: true });
        await writeFile(output, `dependency-${dependencyRuns}`);
      },
    },
    child: {
      name: 'child', dependencies: ['dependency'], lockEntries: ['cmake'], outputs: [childOutput],
      async run({ root: recipeRoot }) {
        childRuns += 1;
        const output = path.join(recipeRoot, childOutput);
        await writeFile(output, `child-${childRuns}`);
      },
    },
  };

  await executeBuildRecipe({ root, recipes, target: 'child' });
  await executeBuildRecipe({ root, recipes, target: 'child' });
  assert.deepEqual([dependencyRuns, childRuns], [1, 1]);

  await writeFile(path.join(root, depOutput), 'tampered');
  await executeBuildRecipe({ root, recipes, target: 'child' });
  assert.deepEqual([dependencyRuns, childRuns], [2, 2]);

  await writeFile(overlay, 'patch-v2');
  await executeBuildRecipe({ root, recipes, target: 'child' });
  assert.deepEqual([dependencyRuns, childRuns], [3, 3]);

  recipes.child = {
    ...recipes.child,
    async run({ root: recipeRoot }) {
      childRuns += 1;
      await writeFile(path.join(recipeRoot, childOutput), `changed-recipe-${childRuns}`);
    },
  };
  await executeBuildRecipe({ root, recipes, target: 'child' });
  assert.deepEqual([dependencyRuns, childRuns], [3, 4]);
  const receipt = await readFile(path.join(root, 'artifacts', 'toolchain', 'receipts', 'child.json'), 'utf8');
  assert.equal(receipt.includes(root), false);
  assert.equal(receipt.includes('generatedAt'), false);
});

test('Browser manifest URL is generated from the matching package versions', async (context) => {
  const { generateBrowserManifestUrl } = await import('../generate-browser-manifest-url.mjs');
  const root = await temporaryRoot(context);
  const browser = path.join(root, 'packages', 'browser');
  const toolchain = path.join(root, 'packages', 'toolchain');
  await mkdir(path.join(browser, 'src'), { recursive: true });
  await mkdir(toolchain, { recursive: true });
  await writeFile(path.join(browser, 'package.json'), '{"name":"@gameguild/emception-browser","version":"9.8.7"}');
  await writeFile(path.join(toolchain, 'package.json'), '{"name":"@gameguild/emception-toolchain","version":"9.8.7"}');

  const result = await generateBrowserManifestUrl(root);
  const generated = await readFile(result.output, 'utf8');
  assert.match(generated, /@gameguild\/emception-toolchain@9\.8\.7\/cdn\/manifest\.json/);

  await writeFile(path.join(toolchain, 'package.json'), '{"name":"@gameguild/emception-toolchain","version":"9.8.8"}');
  await assert.rejects(generateBrowserManifestUrl(root), /Browser 9\.8\.7 does not match Toolchain 9\.8\.8/);
});

test('release recipes can be forced without rebuilding verified tool dependencies', async (context) => {
  const { calculateConfigHash, serializeToolchainLock } = await import('../toolchain/lock.ts');
  const { executeBuildRecipe } = await import('../toolchain/receipts.ts');
  const root = await temporaryRoot(context);
  const config = { schemaVersion: 1, runtimeAbi: 'abi', constraints: { cmake: '<4' }, emsdkGroup: [] };
  const lock = {
    schemaVersion: 1,
    configHash: calculateConfigHash(config),
    tools: {
      cmake: {
        version: '3.31.12',
        source: { kind: 'archive', url: 'https://example.invalid/cmake', sha256: 'c'.repeat(64) },
      },
    },
  };
  await writeFile(path.join(root, 'toolchain', 'toolchain.config.json'), `${JSON.stringify(config, null, 2)}\n`);
  await writeFile(path.join(root, 'toolchain', 'toolchain.lock.json'), serializeToolchainLock(lock));
  const dependencyOutput = path.join('artifacts', 'toolchain', 'tools', 'dependency.wasm');
  const releaseOutput = path.join('artifacts', 'toolchain', 'release', 'cdn', 'manifest.json');
  let dependencyRuns = 0;
  let releaseRuns = 0;
  const releaseForces = [];
  const recipes = {
    dependency: {
      name: 'dependency', dependencies: [], lockEntries: ['cmake'], outputs: [dependencyOutput],
      async run({ root: recipeRoot }) {
        dependencyRuns += 1;
        await mkdir(path.dirname(path.join(recipeRoot, dependencyOutput)), { recursive: true });
        await writeFile(path.join(recipeRoot, dependencyOutput), `dependency-${dependencyRuns}`);
      },
    },
    release: {
      name: 'release', dependencies: ['dependency'], lockEntries: [], outputs: [releaseOutput],
      async run({ root: recipeRoot, force }) {
        releaseForces.push(force);
        releaseRuns += 1;
        await mkdir(path.dirname(path.join(recipeRoot, releaseOutput)), { recursive: true });
        await writeFile(path.join(recipeRoot, releaseOutput), `release-${releaseRuns}`);
      },
    },
  };

  await executeBuildRecipe({ root, recipes, target: 'release' });
  await executeBuildRecipe({ root, recipes, target: 'release', forceRecipes: ['release'] });

  assert.deepEqual([dependencyRuns, releaseRuns], [1, 2]);
  assert.deepEqual(releaseForces, [false, true]);
});
