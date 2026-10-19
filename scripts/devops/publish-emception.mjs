import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { readEmceptionReleaseVersion } from './emception-release-policy.mjs';

export const EMCEPTION_PUBLISH_ORDER = [
  { directory: 'tools/emception/packages/toolchain', name: '@gameguild/emception-toolchain' },
  { directory: 'tools/emception/packages/core', name: 'emception' },
  { directory: 'tools/emception/packages/xterm', name: '@gameguild/emception-xterm' },
  { directory: 'tools/emception/packages/browser', name: '@gameguild/emception-browser' },
  { directory: 'tools/emception/packages/react', name: '@gameguild/emception-react' },
  { directory: 'tools/emception/packages/webcomponent', name: '@gameguild/emception-webcomponent' },
  { directory: 'tools/emception/packages/ide', name: '@gameguild/emception-ide' },
];

const defaultWait = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

function defaultRunCommand(command, args, options = {}) {
  return spawnSync(command, args, {
    cwd: options.cwd,
    env: process.env,
    encoding: 'utf8',
    stdio: options.inherit ? 'inherit' : 'pipe',
  });
}

function assertSuccess(result, description) {
  if (result.status !== 0) {
    throw new Error(`${description} failed with exit ${result.status}: ${result.stderr ?? ''}`.trim());
  }
}

export async function publishEmception(options = {}) {
  const repoRoot = options.repoRoot ?? path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
  const runCommand = options.runCommand ?? defaultRunCommand;
  const wait = options.wait ?? defaultWait;
  const npm = process.platform === 'win32' ? 'npm.cmd' : 'npm';
  const pnpm = process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm';
  const version = await readEmceptionReleaseVersion(repoRoot);

  assertSuccess(
    runCommand(pnpm, ['--dir', 'tools/emception', 'run', 'verify:release'], { cwd: repoRoot, inherit: true }),
    'Emception release verification',
  );

  const packageVersions = new Map();
  for (const entry of EMCEPTION_PUBLISH_ORDER) {
    const manifest = JSON.parse(await readFile(path.join(repoRoot, entry.directory, 'package.json'), 'utf8'));
    if (manifest.name !== entry.name || manifest.version !== version) {
      throw new Error(`Unexpected publish target ${manifest.name}@${manifest.version} in ${entry.directory}`);
    }
    packageVersions.set(entry.name, manifest.version);
    assertSuccess(
      runCommand(npm, ['pack', `./${entry.directory}`, '--dry-run', '--json', '--ignore-scripts'], { cwd: repoRoot }),
      `npm pack ${entry.name}`,
    );
  }

  const published = [];
  for (const entry of EMCEPTION_PUBLISH_ORDER) {
    const spec = `${entry.name}@${packageVersions.get(entry.name)}`;
    const alreadyPublished = runCommand(npm, ['view', spec, 'version'], { cwd: repoRoot }).status === 0;
    if (alreadyPublished) {
      console.log(`[publish-emception] ${spec} is already published; skipping.`);
    } else {
      assertSuccess(
        runCommand(
          npm,
          ['publish', `./${entry.directory}`, '--access', 'public', '--ignore-scripts', '--provenance'],
          { cwd: repoRoot, inherit: true },
        ),
        `npm publish ${spec}`,
      );
      published.push(spec);
      console.log(`New tag: ${spec}`);
    }

    if (entry.name === '@gameguild/emception-toolchain') {
      let visible = false;
      // npm registry CDN propagation for a freshly published scoped package with
      // provenance routinely exceeds 2 minutes; poll up to ~15 minutes before
      // failing the dependent publish.
      for (let attempt = 0; attempt < 90; attempt += 1) {
        if (runCommand(npm, ['view', spec, 'version'], { cwd: repoRoot }).status === 0) {
          visible = true;
          break;
        }
        await wait(10_000);
      }
      if (!visible) throw new Error(`${spec} was not visible in the npm registry before publishing consumers`);
    }
  }

  return { version, published };
}

if (path.resolve(process.argv[1] ?? '') === fileURLToPath(import.meta.url)) {
  publishEmception().catch((error) => {
    console.error(`[publish-emception] ${error instanceof Error ? error.message : String(error)}`);
    process.exitCode = 1;
  });
}
