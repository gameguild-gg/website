'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync } = require('node:fs');
const { createRequire } = require('node:module');
const path = require('node:path');

// API parameter values are strings. Their types come from the pinned engine's
// catalog, rather than truthiness, numeric coercion or guessed JSON parsing.
function decodeParameter(raw, catalogDefault) {
  assert.equal(typeof raw, 'string', 'Expected an exact raw API parameter string');
  if (typeof catalogDefault === 'string') return raw;
  assert(catalogDefault !== undefined && catalogDefault !== null,
    'A parameter needs a supported catalog default type');
  const value = JSON.parse(raw);
  if (Array.isArray(catalogDefault)) {
    assert(Array.isArray(value), 'Expected an array parameter');
  } else if (typeof catalogDefault === 'object') {
    assert(value !== null && typeof value === 'object' && !Array.isArray(value),
      'Expected an object parameter');
  } else {
    assert.equal(typeof value, typeof catalogDefault, 'Parameter type differs from the catalog');
    if (typeof value === 'number') assert(Number.isFinite(value), 'Expected a finite number');
  }
  return value;
}

function projectInput(snapshot, catalog) {
  const definitions = new Map(catalog.patterns.map((row) => [`ESLint8_${row.patternId}`, row]));
  const seen = new Set();
  const patterns = snapshot.patterns.map((row) => {
    assert.equal(row.enabled, true, `${row.patternId}: disabled input is outside this snapshot`);
    assert(!seen.has(row.patternId), `${row.patternId}: duplicate input`);
    seen.add(row.patternId);
    const definition = definitions.get(row.patternId);
    assert(definition, `${row.patternId}: absent from the pinned catalog`);
    const parameters = row.parameters.map((parameter) => {
      const spec = definition.parameters.find((candidate) => candidate.name === parameter.name);
      assert(spec, `${row.patternId}: unknown parameter ${parameter.name}`);
      return { name: parameter.name, value: decodeParameter(parameter.value, spec.default) };
    });
    return { patternId: definition.patternId, parameters };
  });
  assert.equal(patterns.length, snapshot.effectiveEnabledIds);
  return { files: [], tools: [{ name: 'eslint', patterns }], options: {} };
}

function expectedRuleOptions(parameters) {
  const unnamed = parameters.filter((parameter) => parameter.name === 'unnamedParam');
  const named = parameters.filter((parameter) => parameter.name !== 'unnamedParam');
  const result = ['error', ...unnamed.map((parameter) => parameter.value)];
  if (named.length) result.push(Object.fromEntries(named.map(({ name, value }) => [name, value])));
  return result;
}

async function createProjection(snapshot, catalog, engineRequire) {
  const { withDefaultParameters } = engineRequire('codacy-seed/dist/src/withDefaultParameters.js');
  const { createEslintConfig } = engineRequire('/dist/src/configCreator.js');
  const { patternIdToEslint, patternIdToCodacy } = engineRequire('/dist/src/model/patterns.js');
  const input = projectInput(snapshot, catalog);
  const defaultedInput = withDefaultParameters(input, catalog);
  const [options, files] = await createEslintConfig('/src', defaultedInput);
  assert.equal(options.useEslintrc, false, 'Projection must retain the received-pattern mode');

  // The wrapper partitions Storybook patterns into a dedicated override. Its
  // other inherited overrides must not conceal lost or duplicated input rules.
  const storybookOverrides = options.baseConfig.overrides.filter((override) =>
    override.files.some((glob) => glob.startsWith('*.stories.') || glob.startsWith('*.story.')));
  const entries = Object.entries(options.baseConfig.rules).concat(
    storybookOverrides.flatMap((override) => Object.entries(override.rules)));
  const rules = new Map(entries);
  assert.equal(entries.length, rules.size, 'The projected pattern set contains duplicate rule IDs');
  assert.equal(rules.size, snapshot.effectiveEnabledIds, 'The projection lost or added rules');
  for (const pattern of defaultedInput.tools[0].patterns) {
    const ruleId = patternIdToEslint(pattern.patternId);
    assert.equal(patternIdToCodacy(ruleId), pattern.patternId, 'Rule ID encoding is not reversible');
    assert.deepEqual(rules.get(ruleId), expectedRuleOptions(pattern.parameters),
      `${pattern.patternId}: projected options differ from the effective input and catalog defaults`);
  }

  return {
    input,
    options,
    files,
    receipt: {
      eslintVersion: engineRequire('eslint/package.json').version,
      catalogJsonSha256: createHash('sha256').update(JSON.stringify(catalog)).digest('hex'),
      wrapperSourceHashes: Object.fromEntries([
        '/dist/src/configCreator.js',
        '/dist/src/model/patterns.js',
        '/node_modules/codacy-seed/dist/src/withDefaultParameters.js',
      ].map((filename) => [filename, createHash('sha256').update(readFileSync(filename)).digest('hex')])),
      enabledPatterns: input.tools[0].patterns.length,
      projectedRules: rules.size,
      nonemptyParameterSets: snapshot.patterns.filter((row) => row.parameters.length).length,
      rawParameters: snapshot.patterns.reduce((count, row) => count + row.parameters.length, 0),
      rawPatternIdentitySha256: snapshot.patternIdentitySha256,
      allRuleIdsAndOptionsMatched: true,
      inheritedIgnorePatterns: options.baseConfig.ignorePatterns,
      activeScannerChanged: false,
      scope: 'Complete received-pattern projection using the pinned catalog and wrapper. Inherited exclusions and overrides are recorded, not certified as adequate file coverage. Cloud input bytes, contexts and destination closure remain unverified.',
    },
  };
}

module.exports = { decodeParameter, projectInput, createProjection };

if (require.main === module) {
  const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));
  const catalog = JSON.parse(readFileSync('/docs/patterns.json', 'utf8'));
  const engineRequire = createRequire('/node_modules/package.json');
  createProjection(snapshot, catalog, engineRequire).then((projection) => {
    process.stdout.write(`${JSON.stringify(projection, null, 2)}\n`);
  }).catch((error) => {
    process.stderr.write(`${error.stack}\n`);
    process.exitCode = 1;
  });
}
