import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { stageCdnPackage } from './lib/stage-cdn-package.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
// `--optional` (used by the build task) tolerates non-canonical release
// artifacts, e.g. the npm-published cdn pulled into the Docker build, where
// core/cdn is not required for the web build. prepack stays strict.
const optional = process.argv.includes('--optional');

stageCdnPackage({
  sourceCdn: path.join(root, 'artifacts', 'toolchain', 'release', 'cdn'),
  targetCdn: path.join(root, 'packages', 'core', 'cdn'),
}).then((result) => {
  console.log(`[stage-core-cdn] compatibility copy: ${result.bundleCount} bundles, ${result.totalBytes} bytes`);
}).catch((error) => {
  if (optional) {
    console.warn(`[stage-core-cdn] skipped (${error instanceof Error ? error.message : String(error)})`);
    return;
  }
  console.error('[stage-core-cdn] Failed:', error);
  process.exitCode = 1;
});
