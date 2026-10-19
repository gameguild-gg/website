'use strict';

const assert = require('node:assert/strict');
const { createHash } = require('node:crypto');
const { readFileSync, writeFileSync } = require('node:fs');
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

function pinnedEngineModules(engineRequire) {
  const modules = [
    ['@typescript-eslint/parser', '7.18.0', 'dist/index.js', 'c4c3abe0795891ca0deccad6ebc62c95505a3ea5e19eb42ee265d9a1a7e2237b'],
    ['vue-eslint-parser', '9.4.3', 'index.js', 'aaee28693940476c8c399d13a9e8291d4fba6b5b82e4aba2c3980614c5b4a5e8'],
    ['eslint-import-resolver-node', '0.3.9', 'index.js', 'cfd0e50d626259ad3c081fa02379493bb48eef0230363d12c6029b15db5869be'],
    ['eslint-import-resolver-typescript', '3.6.1', 'lib/index.cjs', '92c7061073fac02ddfc55b469a243d180496b107c9ebc9c3e431834df9a43fe4'],
  ];
  return Object.fromEntries(modules.map(([name, version, entry, sha256]) => {
    const filename = engineRequire.resolve(name);
    assert.equal(filename, `/node_modules/${name}/${entry}`, `${name}: unexpected engine module origin`);
    const manifest = JSON.parse(readFileSync(`/node_modules/${name}/package.json`, 'utf8'));
    assert.equal(manifest.version, version, `${name}: engine version drift`);
    assert.equal(createHash('sha256').update(readFileSync(filename)).digest('hex'), sha256,
      `${name}: engine entrypoint drift`);
    return [name, { version, entry: filename, sha256 }];
  }));
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
  const engineModules = pinnedEngineModules(engineRequire);

  return {
    input,
    options,
    files,
    engineModules,
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
      engineModules,
      inheritedIgnorePatterns: options.baseConfig.ignorePatterns,
      activeScannerChanged: false,
      scope: 'Complete received-pattern projection using the pinned catalog and wrapper. Inherited exclusions and overrides are recorded, not certified as adequate file coverage. Cloud input bytes, contexts and destination closure remain unverified.',
    },
  };
}

function readNodeTargets(packageManifestFiles, rootDir = '/src') {
  const targets = [];
  for (const filename of packageManifestFiles) {
    assert(!path.posix.isAbsolute(filename) && !/^[A-Za-z]:/u.test(filename) && !filename.split('/').includes('..')
      && !filename.includes('\\') && path.posix.basename(filename) === 'package.json',
    'A package manifest must be inside the repository');
    const manifest = JSON.parse(readFileSync(path.join(rootDir, filename), 'utf8'));
    const version = manifest.engines?.node;
    if (version === undefined) continue;
    assert(typeof version === 'string' && version.length > 0, `${filename}: invalid engines.node`);
    targets.push({ packageJson: filename, directory: path.posix.dirname(filename), version });
  }
  assert(targets.some((target) => target.packageJson === 'package.json'),
    'The repository must declare its root Node target');
  return targets.sort((left, right) => left.directory.split('/').length - right.directory.split('/').length
    || left.directory.localeCompare(right.directory));
}

function createRepositoryProfile(projection, nodeTargets, typeScriptProject = '/tsconfig.json') {
  assert(typeof typeScriptProject === 'string' && path.isAbsolute(typeScriptProject));
  const root = nodeTargets.find((target) => target.packageJson === 'package.json');
  assert(root, 'The root Node engine declaration is required');
  const settings = projection.options.baseConfig.settings;
  const scopedTargets = nodeTargets.filter((target) => target.packageJson !== 'package.json');
  const modules = projection.engineModules;
  const parser = (name) => {
    assert(modules[name], `Unpinned parser: ${name}`);
    return modules[name].entry;
  };
  const importResolvers = Object.fromEntries(Object.entries(settings['import/resolver']).map(([name, options]) => {
    const module = modules[`eslint-import-resolver-${name}`];
    assert(module, `Unpinned import resolver: ${name}`);
    return [module.entry, options];
  }));
  const parserOverrides = projection.options.baseConfig.overrides
    .filter((override) => override.parser || override.parserOptions?.parser)
    .map((override) => ({
      files: override.files,
      ...(override.excludedFiles ? { excludedFiles: override.excludedFiles } : {}),
      ...(override.parser ? { parser: parser(override.parser) } : {}),
      ...(override.parserOptions?.parser ? { parserOptions: { parser: parser(override.parserOptions.parser) } } : {}),
    }));
  return {
    ...projection.options,
    resolvePluginsRelativeTo: '/',
    // Force inclusion through a supported config-file property. Do not depend
    // on the programmatic ignore:false option, which .eslintrc cannot declare.
    baseConfig: {
      ...projection.options.baseConfig,
      ignorePatterns: ['!**/*'],
      noInlineConfig: true,
      parser: parser(projection.options.baseConfig.parser),
      settings: { ...settings,
        flowtype: { ...settings.flowtype, onlyFilesWithFlowAnnotation: true },
        'import/resolver': importResolvers,
        'import/parsers': Object.fromEntries(Object.entries(settings['import/parsers'])
          .map(([name, extensions]) => [parser(name), extensions])),
        node: { ...settings.node, version: root.version } },
      overrides: [
        ...projection.options.baseConfig.overrides,
        ...scopedTargets.map((target) => ({
          files: [`${target.directory}/**/*`],
          settings: { node: { ...settings.node, version: target.version } },
        })),
        ...parserOverrides,
        // The image's project already allows JavaScript. Its inherited JS
        // override omitted that project while leaving typed plugin rules on.
        // Supply their type services instead of suppressing those rules.
        {
          files: ['*.js', '*.jsx', '*.mjs', '*.cjs', '*.jsm'],
          parserOptions: { project: typeScriptProject,
            extraFileExtensions: [...new Set([
              ...(projection.options.baseConfig.parserOptions.extraFileExtensions ?? []), '.jsm',
            ])] },
        },
        {
          files: ['*.ts', '*.tsx', '*.mts', '*.cts', '*.vue'],
          parserOptions: { project: typeScriptProject },
        },
        // Nonstandard extensions force this pinned parser into watch mode.
        // Ordinary JS/TS sources do not need those extension registrations.
        // Vue and JSM retain the preceding nonstandard-extension settings.
        {
          files: ['*.js', '*.jsx', '*.mjs', '*.cjs', '*.ts', '*.tsx', '*.mts', '*.cts'],
          parserOptions: { extraFileExtensions: [] },
        },
      ],
    },
  };
}

function inferParserSingleRun(parserOptions, engineRequire) {
  const filename = '/node_modules/@typescript-eslint/typescript-estree/dist/parseSettings/inferSingleRun.js';
  assert.equal(createHash('sha256').update(readFileSync(filename)).digest('hex'),
    '8b9c828fd6fb6b45a5b91b89909e5c7cb16d471c2b4f67cb5ef42d4f51eb1115',
    'The pinned parser execution-mode implementation must match');
  return engineRequire(filename).inferSingleRun(parserOptions);
}

function trackedTypeScriptProject(selection, sourceRoot = '/src') {
  assert(typeof sourceRoot === 'string' && path.isAbsolute(sourceRoot));
  assert(Array.isArray(selection.sourceFiles) && selection.sourceFiles.length > 0);
  const checked = selectTrackedFiles(Buffer.from(`${selection.sourceFiles.join('\0')}\0`));
  assert.deepEqual(checked.sourceFiles, selection.sourceFiles,
    'The type project must contain exactly the selected supported inputs');
  return { extends: '/tsconfig.json', files: selection.sourceFiles.map((file) => path.join(sourceRoot, file)),
    include: [], exclude: [] };
}

function writeTrackedTypeScriptProject(selection, directory, sourceRoot = '/src') {
  assert(typeof directory === 'string' && path.isAbsolute(directory));
  const spec = trackedTypeScriptProject(selection, sourceRoot);
  const filename = path.join(directory, 'analysis-tsconfig.json');
  const bytes = Buffer.from(`${JSON.stringify(spec, null, 2)}\n`);
  writeFileSync(filename, bytes);
  return { filename, sha256: createHash('sha256').update(bytes).digest('hex'), rootFileCount: spec.files.length };
}

function selectTrackedFiles(raw) {
  assert(Buffer.isBuffer(raw), 'Expected the raw NUL-delimited Git manifest');
  const text = raw.toString('utf8');
  assert(Buffer.from(text, 'utf8').equals(raw), 'Git paths must roundtrip as UTF-8');
  assert(text.endsWith('\0'), 'Git manifest must retain its final NUL delimiter');
  const files = text.slice(0, -1).split('\0');
  assert(files.length > 0 && files.every(Boolean), 'Git manifest must contain file paths');
  assert.equal(new Set(files).size, files.length, 'Git manifest contains duplicate paths');
  for (const filename of files) {
    assert(!path.posix.isAbsolute(filename) && !/^[A-Za-z]:/u.test(filename)
      && !filename.includes('\\') && !filename.split('/').includes('..'),
    'Git manifest must contain repository-relative POSIX paths');
  }
  // Keep the legacy engine's extensions and the additional JS/TS module
  // extensions covered by the root configuration. No path exclusions apply.
  const extensions = ['js', 'mjs', 'cjs', 'jsx', 'ts', 'mts', 'cts', 'tsx', 'jsm', 'vue'];
  const sourceFiles = files.filter((filename) =>
    extensions.includes(path.posix.extname(filename).slice(1)));
  assert(sourceFiles.length > 0, 'No supported source files were selected');
  return {
    manifestSha256: createHash('sha256').update(raw).digest('hex'),
    trackedFileCount: files.length,
    packageManifestFiles: files.filter((filename) => path.posix.basename(filename) === 'package.json'),
    sourceFileCount: sourceFiles.length,
    extensions,
    sourceFiles,
    directoryOrFileExclusions: [],
    fullLintComplete: false,
    scope: 'Tracked files with supported JS/TS/Vue extensions. Other languages remain owned by their scanners; this inventory is not a completed lint result.',
  };
}

module.exports = {
  decodeParameter, projectInput, createProjection, createRepositoryProfile, selectTrackedFiles, readNodeTargets,
  inferParserSingleRun,
  trackedTypeScriptProject, writeTrackedTypeScriptProject,
};

if (require.main === module) {
  const snapshot = JSON.parse(readFileSync(path.join(__dirname, 'effective-patterns.json'), 'utf8'));
  const catalog = JSON.parse(readFileSync('/docs/patterns.json', 'utf8'));
  const engineRequire = createRequire('/node_modules/package.json');
  createProjection(snapshot, catalog, engineRequire).then((projection) => {
    assert(process.env.GAMEGUILD_CODACY_TRACKED_FILES, 'A complete tracked-file manifest is required');
    projection.selection = selectTrackedFiles(readFileSync(process.env.GAMEGUILD_CODACY_TRACKED_FILES));
    projection.nodeTargets = readNodeTargets(projection.selection.packageManifestFiles);
    projection.trackedTypeScriptProject = trackedTypeScriptProject(projection.selection);
    projection.repositoryOptions = createRepositoryProfile(projection, projection.nodeTargets,
      '/evidence/analysis-tsconfig.json');
    process.stdout.write(`${JSON.stringify(projection, null, 2)}\n`);
  }).catch((error) => {
    process.stderr.write(`${error.stack}\n`);
    process.exitCode = 1;
  });
}
