'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync, realpathSync, writeFileSync } = require('node:fs');
const path = require('node:path');

// Repair only observed failures in the pinned Codacy image. Package
// versions and complete source hashes are checked before any file is written.
// The image retains each upstream package's license and the rest of its source.
const repairs = [
  {
    package: 'eslint-plugin-import',
    version: '2.29.1',
    file: 'lib/rules/no-namespace.js',
    sha256: 'd45eb7bb1b34a36150d5f40491698bd1e34031ecb464a5fa1bdd93b2ceac5694',
    repairedSha256: '5d9065a612f13a901bcefe907ea5997eb651dabb1a4ea9f1477924c12ae19a88',
    replacements: [
      ['variable.defs[0].node === node', 'variable.defs[0]?.node === node'],
      ['var namespaceReferences = namespaceVariable.references;',
        'var namespaceReferences = namespaceVariable?.references ?? [];'],
    ],
  },
  {
    package: '@shopify/eslint-plugin',
    version: '45.0.0',
    file: 'lib/rules/jest/no-all-mocks-methods.js',
    sha256: '88f51f3fb685f90ad17e9dd79f411d94b76fb4d2caf4ea2b80aad29165d608db',
    repairedSha256: 'dc3ad06aedfa855ed3fd2c3ff2be00552870627f5b96f629181dc475e82d9a25',
    replacements: [
      ["  },\n  messages: {\n    allMocksMethod:\n      'Do not use {{method}} or related methods that are not explicit to a single mock. Instead, clear, reset and restore mocks individually.',\n  },",
        "    messages: {\n      allMocksMethod:\n        'Do not use {{method}} or related methods that are not explicit to a single mock. Instead, clear, reset and restore mocks individually.',\n    },\n  },"],
    ],
  },
  {
    package: 'eslint-plugin-security-node',
    version: '1.1.4',
    file: 'lib/rules/detect-unhandled-async-errors.js',
    sha256: 'd7edbaed8f6c13a222f1402ffbe9ca926ec9d359106ef0e02b96955bed7deeb2',
    repairedSha256: '0665c8f495d164459e279dc6cd9063b741e3d78e2634195a4afb27e9d52a31e7',
    replacements: [
      ["bodyArg.handler.type === 'CatchClause' &&",
        "bodyArg.handler?.type === 'CatchClause' &&"],
    ],
  },
  {
    package: '@shopify/eslint-plugin',
    version: '45.0.0',
    file: 'lib/rules/prefer-class-properties.js',
    sha256: 'a9ab19cc5ade3451d6795edede0c7ef07b560bcc93aa0e60ea98d117ace25d13',
    repairedSha256: '818de3925f96bd9fa56a839e3304ab3efc1b1bef03530b74815799f054263077',
    replacements: [
      ['return functionNode.body.body', 'return (functionNode?.body?.body ?? [])'],
    ],
  },
  {
    package: 'eslint-plugin-react-native',
    version: '4.1.0',
    file: 'lib/rules/no-raw-text.js',
    sha256: '60221ed8f4cb6776def089fa988f89122e645d04046b3159a5a573984bb3ab25',
    repairedSha256: 'bcdf65e489312f0d6ec128bd3252a84834279d4712462c669ece30870b786991',
    replacements: [
      ['`TemplateLiteral: ${node.expressions[0].name}`',
        '`TemplateLiteral: ${node.expressions[0]?.name ?? node.quasis[0].value.raw}`'],
    ],
  },
  {
    package: 'eslint-plugin-flowtype',
    version: '8.0.3',
    file: 'dist/rules/typeImportStyle.js',
    sha256: 'f52ddfaf238d9f9c6bad8484b34c5c52daf97c0cd5e382e5013fb647560de32f',
    repairedSha256: '8fdb7b6237f0a94b8d57cd10db5612bec587360dc2acd17a69fb58c0b89e6cf1',
    replacements: [
      ['        fix(fixer) {',
        "        fix(fixer) {\n          if (node.specifiers.some(specifier => specifier.type === 'ImportNamespaceSpecifier')) return null;"],
    ],
  },
];

function digest(value) {
  return createHash('sha256').update(value).digest('hex');
}

function prepareRepair(engineModules, repair) {
  const packageRoot = realpathSync(path.join(engineModules, repair.package));
  const sourcePath = realpathSync(path.join(packageRoot, repair.file));
  const relative = path.relative(packageRoot, sourcePath);
  assert(!relative.startsWith('..') && !path.isAbsolute(relative), repair.file);
  const manifest = JSON.parse(readFileSync(path.join(packageRoot, 'package.json'), 'utf8'));
  assert.equal(manifest.version, repair.version, `${repair.package}: package drift`);
  const original = readFileSync(sourcePath);
  assert.equal(digest(original), repair.sha256, `${repair.package}: source drift`);
  let source = original.toString('utf8');
  for (const [before, after] of repair.replacements) {
    assert.equal(source.split(before).length - 1, 1, `${repair.package}: ambiguous repair`);
    source = source.replace(before, after);
  }
  assert.equal(digest(source), repair.repairedSha256, `${repair.package}: repaired source drift`);
  return { repair, sourcePath, source, repairedSha256: digest(source) };
}

function repairEngine(engineModules) {
  assert(path.isAbsolute(engineModules), 'Pass an absolute image node_modules path');
  const prepared = repairs.map((repair) => prepareRepair(engineModules, repair));
  // Preflight all packages first. A drifted package must not leave a partially
  // repaired engine that could be mistaken for the reviewed image.
  for (const item of prepared) writeFileSync(item.sourcePath, item.source, 'utf8');
  return prepared.map(({ repair, sourcePath, repairedSha256 }) => ({
    package: repair.package,
    version: repair.version,
    file: repair.file,
    originalSha256: repair.sha256,
    repairedSha256,
    readbackSha256: digest(readFileSync(sourcePath)),
  }));
}

module.exports = { repairs, repairEngine };

if (require.main === module) {
  const receipt = repairEngine(process.argv[2]);
  for (const item of receipt) assert.equal(item.readbackSha256, item.repairedSha256);
  process.stdout.write(`${JSON.stringify({ repairedPackages: receipt }, null, 2)}\n`);
}
