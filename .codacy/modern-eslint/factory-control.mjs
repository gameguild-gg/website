import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { copyFileSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import loaderProvenance from './loader-provenance.cjs';

// This diagnostic container receives only this script, the public lint config
// and an empty evidence directory. Every analyzed input below is synthetic.
const evidence = path.resolve(process.argv[2]);
assert.equal(evidence, '/factory-evidence');
const configInput = '/probe/root-eslint.config.mjs';
const digest = (bytes) => createHash('sha256').update(bytes).digest('hex');
const engineRequire = createRequire('/app/package.json');
const report = {
  sourceHead: process.env.GAMEGUILD_CODACY_SOURCE_HEAD,
  scope: 'Synthetic cold factory diagnostic only; no repository source, Git directory, host environment or credential is mounted.',
  scriptSha256: digest(readFileSync(fileURLToPath(import.meta.url))),
  configSha256: digest(readFileSync(configInput)),
  repositoryInputs: 0,
  warningOutput: loaderProvenance.assertOriginalWarningOutput(),
  cases: [],
  complete: false,
  certifiedDestinationClosures: 0,
};
assert.match(report.sourceHead, /^[a-f0-9]{40}$/u);
mkdirSync(evidence, { recursive: true });
const write = () => writeFileSync(path.join(evidence, 'synthetic-factory.json'), `${JSON.stringify(report, null, 2)}\n`);
write();

try {
  report.loader = loaderProvenance.readLoaderIdentity();
  const root = mkdtempSync(path.join(tmpdir(), 'codacy-synthetic-factory-'));
  mkdirSync(path.join(root, 'packages/tooling/eslint'), { recursive: true });
  writeFileSync(path.join(root, 'packages/tooling/eslint/package.json'),
    '{"name":"synthetic-lint-anchor","private":true,"type":"module"}\n');
  copyFileSync(configInput, path.join(root, 'eslint.config.mjs'));
  const controls = [
    { file: 'control.mjs', source: 'export const control = 1;\n', rules: [] },
    { file: 'undefined-control.mjs', source: 'export const control = syntheticMissingName;\n', rules: ['no-undef'] },
    { file: 'jsx-control.tsx', source: 'export const Control = () => <span />;\n', rules: [] },
  ];
  for (const control of controls) writeFileSync(path.join(root, control.file), control.source);
  process.chdir(root);

  // Exercise real loader imports through both existing and missing filesystem
  // entries. These public contracts cover every nullable-stat call site.
  const resolutionRoot = path.join(root, 'resolution');
  mkdirSync(path.join(resolutionRoot, 'directory'), { recursive: true });
  mkdirSync(path.join(resolutionRoot, 'node_modules/probe-package'), { recursive: true });
  writeFileSync(path.join(resolutionRoot, 'present.mjs'), 'export default 42;\n');
  writeFileSync(path.join(resolutionRoot, 'directory/index.mjs'), 'export default 43;\n');
  writeFileSync(path.join(resolutionRoot, 'node_modules/probe-package/package.json'),
    '{"name":"probe-package","type":"module","main":"index.mjs"}\n');
  writeFileSync(path.join(resolutionRoot, 'node_modules/probe-package/index.mjs'), 'export default 44;\n');
  const resolutions = [
    { name: 'existing-file', specifier: './present.mjs', expected: { value: 42 } },
    { name: 'missing-file', specifier: './missing.mjs', expected: {
      errorName: 'Error', errorCode: null,
      errorMessage: "Cannot find module '<synthetic-root>/resolution/missing.mjs' imported from <synthetic-root>/resolution/missing-file.mjs",
    } },
    { name: 'directory-import', specifier: './directory', expected: {
      errorName: 'Error', errorCode: null,
      errorMessage: 'ERR_UNSUPPORTED_DIR_IMPORT <synthetic-root>/resolution/directory <synthetic-root>/resolution/directory-import.mjs',
    } },
    { name: 'explicit-directory-index', specifier: './directory/index.mjs', expected: { value: 43 } },
    { name: 'package-main', specifier: 'probe-package', expected: { value: 44 } },
    { name: 'missing-package', specifier: 'probe-package-missing', expected: {
      errorName: 'Error', errorCode: null,
      errorMessage: "Cannot find package 'probe-package-missing' imported from <synthetic-root>/resolution/missing-package.mjs",
    } },
  ];
  report.resolutions = [];
  for (const control of resolutions) {
    const filename = path.join(resolutionRoot, `${control.name}.mjs`);
    const source = `export { default } from ${JSON.stringify(control.specifier)};\n`;
    writeFileSync(filename, source);
    let outcome;
    try {
      outcome = { value: (await import(pathToFileURL(filename).href)).default };
    } catch (error) {
      // This pinned ts-node defines CustomError classes without a code property.
      // Preserve the exact native name/message, normalizing only the random root.
      outcome = { errorName: error?.name, errorCode: error?.code ?? null,
        errorMessage: typeof error?.message === 'string' ? error.message.replaceAll(root, '<synthetic-root>') : null };
    }
    assert.deepEqual(outcome, control.expected, control.name);
    report.resolutions.push({ name: control.name, inputSha256: digest(source), outcome, passed: true });
  }

  const versions = { eslint: '9.39.5', typescript: '6.0.3', '@typescript-eslint/parser': '8.64.0' };
  report.identities = Object.fromEntries(Object.entries(versions).map(([name, expected]) => {
    const bytes = readFileSync(engineRequire.resolve(`${name}/package.json`));
    const version = JSON.parse(bytes).version;
    assert.equal(version, expected);
    return [name, { version, sha256: digest(bytes) }];
  }));
  const factoryPath = '/app/workspaces/codacy/src/configCreator.ts';
  report.factorySha256 = digest(readFileSync(factoryPath));
  assert.equal(report.factorySha256, '75b27a0bf89655b6dfc9c94b3d9c1936862530b3196f9de78d57f64609f15858');
  const { createEslintConfig } = await import(factoryPath);
  // Await the vendor's complete plugin initialization so delayed import output
  // remains inside this synthetic diagnostic's stdout/stderr evidence.
  const { getAll } = await import('/app/lib/models/plugins.ts');
  report.initializedPlugins = (await getAll()).length;
  assert(report.initializedPlugins > 0);
  const selected = controls.map((control) => control.file);
  const [options, files] = await createEslintConfig(root, {
    files: selected, tools: [{ name: 'eslint-9', patterns: [] }], options: {},
  });
  assert.deepEqual(files, selected);
  assert.equal(options.overrideConfigFile, path.join(root, 'eslint.config.mjs'));
  report.rootConfigurationSelectedByFactory = true;
  report.factoryOptionsSha256 = digest(JSON.stringify({ ...options, overrideConfigFile: '<synthetic-root>/eslint.config.mjs' }));
  const { ESLint } = engineRequire('eslint');
  const eslint = new ESLint({ ...options, cwd: root, ignore: false, allowInlineConfig: false });
  for (const control of controls) {
    const config = await eslint.calculateConfigForFile(path.join(root, control.file));
    const rules = Object.fromEntries(Object.entries(config.rules).sort(([a], [b]) => a.localeCompare(b)));
    const [result] = await eslint.lintFiles([path.join(root, control.file)]);
    assert.equal(result.fatalErrorCount, 0);
    assert.deepEqual(result.messages.map((message) => message.ruleId), control.rules);
    report.cases.push({ file: control.file, inputSha256: digest(control.source), rules: control.rules,
      effectiveRulesSha256: digest(JSON.stringify(rules)), parser: config.languageOptions.parser.meta,
      sourceType: config.languageOptions.sourceType, passed: true });
  }
  report.warningOutputAfterFactory = loaderProvenance.assertOriginalWarningOutput();
  report.complete = true;
} finally {
  write();
}
