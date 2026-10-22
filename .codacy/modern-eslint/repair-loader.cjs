'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync, realpathSync, writeFileSync } = require('node:fs');
const path = require('node:path');

const repair = {
  package: 'ts-node', version: '10.9.2', file: 'dist-raw/node-internal-modules-esm-resolve.js',
  packageSha256: '80961faab4990ab53d33976bf716aa32444d70c64f73a487c23c21b0ca81398e',
  originalSha256: '205cce0aabf93b76c805bebaf092f57900e2a458089805ca3009239f6a649365',
  repairedSha256: '597100e286a0b95b3d5d742d138b37a4784056eac23491a4e8eccb89debcfd3d',
  replacements: [
    ['  Stats,\n', ''],
    ['const statsIfNotFound = new Stats();\n', ''],
    ['  const stats = statSync(path, { throwIfNoEntry: false });\n  if(stats != null) return stats;\n  return statsIfNotFound;',
      '  return statSync(path, { throwIfNoEntry: false });'],
    ['    return statsIfNotFound;', '    return undefined;'],
    ['return tryStatSync(fileURLToPath(url)).isFile();', 'return tryStatSync(fileURLToPath(url))?.isFile() ?? false;'],
    ['if (stats.isDirectory()) {', 'if (stats?.isDirectory()) {'],
    ['} else if (!stats.isFile()) {', '} else if (!stats?.isFile()) {'],
    ['if (!stat.isDirectory()) {', 'if (!stat?.isDirectory()) {'],
  ],
};
const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');

function repairLoader(modules, receiptPath) {
  assert(path.isAbsolute(modules) && path.isAbsolute(receiptPath));
  const root = realpathSync(path.join(modules, repair.package));
  const filename = realpathSync(path.join(root, repair.file));
  const relative = path.relative(root, filename);
  assert(!relative.startsWith('..') && !path.isAbsolute(relative));
  const manifest = readFileSync(path.join(root, 'package.json'));
  assert.equal(digest(manifest), repair.packageSha256, 'Loader package drift');
  assert.equal(JSON.parse(manifest).version, repair.version);
  const original = readFileSync(filename);
  assert.equal(digest(original), repair.originalSha256, 'Loader source drift');
  let source = original.toString('utf8');
  for (const [before, after] of repair.replacements) {
    assert.equal(source.split(before).length - 1, 1, 'Ambiguous loader repair');
    source = source.replace(before, after);
  }
  assert.equal(digest(source), repair.repairedSha256, 'Repaired source drift');
  writeFileSync(filename, source, 'utf8');
  const receipt = {
    ...Object.fromEntries(Object.entries(repair).filter(([key]) => key !== 'replacements')),
    readbackSha256: digest(readFileSync(filename)),
    repairScriptSha256: digest(readFileSync(__filename)),
    changedFiles: [repair.file],
  };
  assert.equal(receipt.readbackSha256, repair.repairedSha256);
  writeFileSync(receiptPath, `${JSON.stringify(receipt, null, 2)}\n`);
  return receipt;
}

module.exports = { repair, repairLoader };
if (require.main === module) repairLoader(process.argv[2], process.argv[3]);
