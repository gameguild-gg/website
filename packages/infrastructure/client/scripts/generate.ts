/**
 * @game-guild/client - Code Generation Pipeline
 *
 * Main entry point for generating typed API client from OpenAPI specification.
 *
 * Usage:
 *   pnpm generate                    # Generate from default API URL
 *   OPENAPI_URL=... pnpm generate    # Generate from custom URL
 *   pnpm generate -- --openapi spec.json # Generate from captured artifact
 *   pnpm generate --watch            # Watch mode (re-generate on spec changes)
 */

import { createHash } from 'crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'fs';
import { userInfo } from 'os';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

import { fetchOpenApiSpec, type OpenApiSpec } from './fetch-spec.js';
import { normalizeSpec } from './normalize.js';
import { generateTypes } from './codegen/types.js';
import { generateEndpoints } from './codegen/endpoints.js';
import { generateErrors } from './codegen/errors.js';
import { generateModules } from './codegen/modules.js';
import { formatOutput } from './utils/formatting.js';
import { cleanGeneratedOutput } from './utils/clean-generated-output.js';
import { safeJoin } from './utils/safe-join.js';
import { resolveGeneratorConfig } from './config.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const generatorConfig = resolveGeneratorConfig(process.argv.slice(2), process.env);

const CONFIG = {
  openApiSource: generatorConfig.openApiSource,
  generatedSourceLabel: generatorConfig.generatedSourceLabel,
  outputDir: join(__dirname, '..', 'src', 'generated'),
  metadataFile: join(__dirname, '..', 'src', 'generated', '.metadata.json'),
  watch: generatorConfig.watch,
  force: generatorConfig.force,
};

interface GeneratorMetadata {
  hash: string;
  apiVersion: string | undefined;
  source: string;
  generatedAt: string;
  generatedBy: string;
}

/**
 * Calculate SHA256 hash of the OpenAPI spec for change detection
 */
function calculateHash(spec: OpenApiSpec): string {
  return createHash('sha256')
    .update(JSON.stringify(spec, null, 0))
    .digest('hex');
}

/**
 * Load previous generation metadata
 */
function loadMetadata(): GeneratorMetadata | null {
  if (!existsSync(CONFIG.metadataFile)) {
    return null;
  }
  try {
    return JSON.parse(readFileSync(CONFIG.metadataFile, 'utf-8'));
  } catch {
    return null;
  }
}

/**
 * Metadata written before provenance tracking existed lacks generatedAt /
 * generatedBy; treat it as stale so the next run migrates it.
 */
function hasProvenanceFields(metadata: GeneratorMetadata | null): boolean {
  return metadata !== null && metadata.generatedAt !== undefined && metadata.generatedBy !== undefined;
}

/**
 * Resolve the user running the generator. os.userInfo() works on POSIX and
 * Windows without a shell; it can throw on POSIX without a passwd entry, so
 * fall back to USER / USERNAME before giving up.
 */
function resolveGeneratorUser(): string {
  try {
    const username = userInfo().username;
    if (username) {
      return username;
    }
  } catch {
    // userInfo() unavailable — use environment lookup below.
  }

  return process.env.USER ?? process.env.USERNAME ?? 'unknown';
}

/**
 * Save generation metadata for incremental builds
 */
function saveMetadata(hash: string, spec: OpenApiSpec): void {
  // generatedAt/generatedBy are provenance and intentionally differ per run;
  // diffing consumers must ignore them.
  const metadata: GeneratorMetadata = {
    hash,
    apiVersion: spec.info?.version,
    source: CONFIG.generatedSourceLabel,
    generatedAt: new Date().toISOString(),
    generatedBy: resolveGeneratorUser(),
  };

  if (!existsSync(dirname(CONFIG.metadataFile))) {
    mkdirSync(dirname(CONFIG.metadataFile), { recursive: true });
  }

  writeFileSync(CONFIG.metadataFile, JSON.stringify(metadata, null, 2));
}

/**
 * Ensure output directory exists
 */
function ensureOutputDir(): void {
  if (!existsSync(CONFIG.outputDir)) {
    mkdirSync(CONFIG.outputDir, { recursive: true });
  }

  // Create modules subdirectory
  const modulesDir = join(CONFIG.outputDir, 'modules');
  if (!existsSync(modulesDir)) {
    mkdirSync(modulesDir, { recursive: true });
  }
}

/**
 * Write generated file with formatting
 */
async function writeGeneratedFile(filename: string, content: string): Promise<void> {
  // Filename embeds OpenAPI tag names (external data) — keep writes inside the output dir.
  const filepath = safeJoin(CONFIG.outputDir, filename);
  const formatted = await formatOutput(content, filepath);
  writeFileSync(filepath, formatted);
  console.log(`  ✓ Generated ${filename}`);
}

/**
 * Main generation pipeline
 */
async function generate(): Promise<void> {
  console.log('🚀 @game-guild/client Code Generator\n');
  console.log(`📡 Fetching OpenAPI spec from: ${CONFIG.openApiSource}`);

  try {
    // Step 1: Fetch OpenAPI specification
    const rawSpec = await fetchOpenApiSpec(CONFIG.openApiSource);
    const spec = normalizeSpec(rawSpec);
    const currentHash = calculateHash(spec);

    // Step 2: Check if regeneration is needed
    const metadata = loadMetadata();
    if (!CONFIG.force && metadata?.hash === currentHash && hasProvenanceFields(metadata)) {
      console.log('✅ API spec unchanged, skipping generation\n');
      return;
    }

    console.log(`📋 API Version: ${rawSpec.info?.version || 'unknown'}`);
    console.log(`🔄 Spec hash: ${currentHash.slice(0, 12)}...\n`);

    // Step 3: Remove outputs from endpoints that no longer exist in the specification.
    cleanGeneratedOutput(CONFIG.outputDir);

    // Step 4: The specification was normalized before hashing so both the generated output and
    // incremental metadata are independent of JSON object key order.
    console.log('🔧 OpenAPI spec normalized...');

    // Step 5: Ensure output directory exists
    ensureOutputDir();

    // Step 6: Generate code
    console.log('📝 Generating TypeScript code...');

    // Generate types (DTOs, models, enums)
    const typesCode = generateTypes(spec);
    await writeGeneratedFile('types.gen.ts', typesCode);

    // Generate error types
    const errorsCode = generateErrors(spec);
    await writeGeneratedFile('errors.gen.ts', errorsCode);

    // Generate endpoint definitions
    const endpointsCode = generateEndpoints(spec);
    await writeGeneratedFile('endpoints.gen.ts', endpointsCode);

    // Generate module-grouped endpoints
    const modules = generateModules(spec);
    for (const [moduleName, moduleCode] of Object.entries(modules)) {
      await writeGeneratedFile(`modules/${moduleName}.gen.ts`, moduleCode);
    }

    // Generate index file
    const indexCode = generateIndex(Object.keys(modules));
    await writeGeneratedFile('index.ts', indexCode);

    // Step 7: Save metadata
    saveMetadata(currentHash, rawSpec);

    console.log('\n✅ Code generation complete!\n');
  } catch (error) {
    console.error('\n❌ Generation failed:', error instanceof Error ? error.message : error);
    // Watch mode stays alive — the API may still be booting; the poll retries.
    if (!CONFIG.watch) process.exit(1);
  }
}

/**
 * Generate index file that re-exports all generated code
 */
function generateIndex(moduleNames: string[]): string {
  const moduleExports = moduleNames.map((name) => `export * from './modules/${name}.gen.js';`).join('\n');

  return `/**
 * @game-guild/client - Generated API Types and Endpoints
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 *
 * Generated from: ${CONFIG.generatedSourceLabel}
 *
 * To regenerate, run: pnpm generate
 */

// Re-export all types
export * from './types.gen.js';

// Re-export error types
export * from './errors.gen.js';

// Re-export endpoint definitions
export * from './endpoints.gen.js';

// Re-export module-grouped endpoints
${moduleExports}
`;
}

/**
 * Watch mode - poll for spec changes
 */
async function watchMode(): Promise<void> {
  console.log('👀 Watch mode enabled - polling for changes every 10s\n');

  // Initial generation
  await generate();

  // Poll for changes
  setInterval(async () => {
    try {
      await generate();
    } catch {
      // Ignore errors in watch mode, will retry
    }
  }, 10_000);
}

// Entry point
if (CONFIG.watch) {
  watchMode().catch(console.error);
} else {
  generate().catch(console.error);
}
