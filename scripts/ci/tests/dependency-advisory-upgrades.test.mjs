import assert from 'node:assert/strict';
import { readFileSync, realpathSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const webRequire = createRequire(join(root, 'apps/web/package.json'));
const nextRequire = createRequire(webRequire.resolve('next/package.json'));
const sharp = nextRequire('sharp');
const rootRequire = createRequire(join(root, 'package.json'));
const scriptsRequire = createRequire(rootRequire.resolve('npm-run-all/package.json'));
const shellQuote = scriptsRequire('shell-quote');
const shadcnRequire = createRequire(realpathSync(join(root, 'packages/ui/node_modules/shadcn/package.json')));

function packageVersion(entry, expectedName) {
  let directory = dirname(entry);
  while (directory !== dirname(directory)) {
    try {
      const manifest = JSON.parse(readFileSync(join(directory, 'package.json'), 'utf8'));
      if (manifest.name === expectedName) {
        return manifest.version;
      }
    } catch (error) {
      if (error.code !== 'ENOENT') {
        throw error;
      }
    }
    directory = dirname(directory);
  }
  throw new Error(`Cannot identify the installed package ${expectedName}`);
}

test('security overrides select the corrected packages used by Next, scripts and shadcn', () => {
  assert.equal(sharp.versions.sharp, '0.35.5');
  assert.equal(packageVersion(scriptsRequire.resolve('shell-quote'), 'shell-quote'), '1.11.0');
  assert.equal(packageVersion(shadcnRequire.resolve('@modelcontextprotocol/sdk/client/auth.js'), '@modelcontextprotocol/sdk'), '1.31.0');
});

test('the updated native image dependency renders a benign SVG to PNG', async () => {
  const result = await sharp(Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="2" height="3"><rect width="2" height="3" fill="blue"/></svg>'))
    .png().toBuffer({ resolveWithObject: true });
  assert.equal(result.info.format, 'png');
  assert.equal(result.info.width, 2);
  assert.equal(result.info.height, 3);
  assert.deepEqual([...result.data.subarray(0, 8)], [137, 80, 78, 71, 13, 10, 26, 10]);
});

for (const [name, separator] of [['LF', '\n'], ['CR', '\r'], ['line separator', '\u2028'], ['paragraph separator', '\u2029']]) {
  test(`shell quoting rejects ${name} in a string following a comment token`, () => {
    assert.throws(() => shellQuote.quote(['echo', 'ok', { comment: 'marker' }, `a${separator}synthetic;#`]), TypeError);
  });
}

test('ordinary shell quoting still preserves literal arguments', () => {
  const values = ['echo', 'two words', '#literal', '$literal', "a'b"];
  assert.deepEqual(shellQuote.parse(shellQuote.quote(values)), values);
});
