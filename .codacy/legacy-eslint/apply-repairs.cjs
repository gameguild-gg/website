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
    repairedSha256: '250c0617369c25d6ace77164f7b8d8913de07368d75846b8c550b69c0987bd7f',
    replacements: [
      ['return functionNode.body.body', 'return (functionNode?.body?.body ?? [])'],
      ['    function isSimpleLiteral(node) {\n      return (',
        '    function isSimpleLiteral(node) {\n      return node !== undefined && (\n        node === null ||'],
    ],
  },
  {
    package: 'eslint-plugin-es-x',
    version: '7.8.0',
    file: 'lib/util/define-prototype-method-handler/object-type-checker-for-ts.js',
    sha256: '92afb21c6ea936cbb996d1b370e94a32f6e6eecabf00f448073fb0b034f9bf14',
    repairedSha256: 'd1509a6531948990aff20b47ba1945bfd51ed984adaa8837b46ed4da6bc0652f',
    replacements: [
      ['            for (const declaration of declarations) {\n                const type =',
        '            for (const declaration of declarations) {\n                if (!declaration.parent || !declaration.parent.parent) {\n                    continue\n                }\n                const type ='],
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
  {
    package: 'eslint-plugin-lodash-fp',
    // The registry normalizes this to 2.2.0-a1; the verified tar manifest is literal.
    version: '2.2.0a1',
    file: 'rules/core/enhance.js',
    sha256: '96e3be11a9afe9adc24ad12452d9207e414084286b6d68cc215062b02825ba5e',
    repairedSha256: '70c44a9554ec4d9f4334ed9d5dd75d04724877f8db453da1314067a27200afc5',
    replacements: [
      ['const imports = {};', 'const imports = Object.create(null);'],
    ],
  },
  {
    package: 'eslint-plugin-jsx-a11y',
    version: '6.10.2',
    file: 'lib/rules/prefer-tag-over-role.js',
    sha256: '5e0a48198a6b43d3b315de4cbaa13e85f10cea3d3c893bd0609adb1757eba1e2',
    repairedSha256: '2301c5e5892f8d583d9ca56e9f878c591ee25a516ab454815d1cc0b90ea343bf',
    replacements: [
      ['  if (!propValue) {', "  if (typeof propValue !== 'string' || !propValue) {"],
    ],
  },
  {
    package: 'eslint-plugin-perfectionist',
    version: '3.2.0',
    file: 'dist/rules/sort-jsx-props.js',
    sha256: '76884ff384c8b3b7076908bfb25146b19556af3a0ba9a118e299fb352d5b5a30',
    repairedSha256: '752e68fc6199707f9aba472713e6dbe6832b30e6966b240e98d81338d1c35eaf',
    replacements: [
      ['                    return makeFixes.makeFixes(',
        '                    const fixes = makeFixes.makeFixes('],
      ['                      sourceCode,\n                    )',
        '                      sourceCode,\n                    )\n                    const ordered = [...fixes].sort((a, b) => a.range[0] - b.range[0] || a.range[1] - b.range[1])\n                    if (ordered.some((fix, index) => index > 0 && ordered[index - 1].range[1] > fix.range[0])) return null\n                    return fixes'],
    ],
  },
  {
    package: 'eslint-plugin-vue',
    version: '9.32.0',
    file: 'lib/rules/max-len.js',
    sha256: 'aea8ccbe5091f53bb0b97cae1c0b081ef0ce5abe712d0d46ddd24bb4adadd38d',
    repairedSha256: 'a7587695860051aff311d0ce253a7b3ae764fab752b7914e43479ca7908b1959',
    replacements: [
      ['const OPTIONS_SCHEMA = {',
        'function appendItems(target, ...sources) {\n  for (const items of sources) {\n    for (const item of items) target.push(item)\n  }\n}\n\nconst OPTIONS_SCHEMA = {'],
      ['tokens.push(...templateTokens, ...scriptTokens)',
        'appendItems(tokens, templateTokens, scriptTokens)'],
      ['tokens.push(...scriptTokens, ...templateTokens)',
        'appendItems(tokens, scriptTokens, templateTokens)'],
      ['tokens.push(...scriptTokens)', 'appendItems(tokens, scriptTokens)'],
      ['comments.push(...templateBody.comments, ...scriptComments)',
        'appendItems(comments, templateBody.comments, scriptComments)'],
      ['comments.push(...scriptComments, ...templateBody.comments)',
        'appendItems(comments, scriptComments, templateBody.comments)'],
      ['comments.push(...scriptComments)', 'appendItems(comments, scriptComments)'],
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
