import { mkdir, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';

export async function writeBrowserEvidence(outputPath, result) {
  if (!outputPath) return;

  for (const key of ['file', 'title', 'projectName', 'startedAt']) {
    if (typeof result[key] !== 'string' || result[key].trim().length === 0) {
      throw new TypeError(`Browser evidence requires ${key}`);
    }
  }
  if (!Number.isFinite(Date.parse(result.startedAt))) {
    throw new TypeError('Browser evidence requires a valid startedAt');
  }
  if (!Number.isFinite(result.durationMs) || result.durationMs < 0) {
    throw new TypeError('Browser evidence requires a nonnegative durationMs');
  }
  if (typeof result.passed !== 'boolean' || !Array.isArray(result.errors)) {
    throw new TypeError('Browser evidence requires a boolean outcome and an errors array');
  }
  const passed = result.passed && result.errors.length === 0;
  const evidence = {
    metadata: { producer: 'browser-smoke-script', schemaVersion: 2 },
    stats: {
      expected: passed ? 1 : 0,
      unexpected: passed ? 0 : 1,
      skipped: 0,
    },
    errors: result.errors,
    suites: [{
      title: result.file,
      file: result.file,
      suites: [],
      specs: [{
        id: `${result.file}:${result.title}`,
        title: result.title,
        file: result.file,
        ok: passed,
        tests: [{
          projectName: result.projectName,
          expectedStatus: 'passed',
          status: passed ? 'expected' : 'unexpected',
          results: [{
            status: passed ? 'passed' : 'failed',
            startTime: result.startedAt,
            duration: result.durationMs,
            retry: 0,
            errors: result.errors.map((message) => ({ message: String(message) })),
          }],
        }],
      }],
    }],
  };

  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
}

const socialMetadataKeys = [
  'journey',
  'startedAt',
  'completedAt',
  'durationMs',
  'apiBaseUrl',
  'webBaseUrl',
  'tenantId',
  'actorIds',
  'runTag',
];

export async function writeSocialFeedEvidence(outputPath, result) {
  const requiredCapabilities = result.requiredCapabilities ?? Object.keys(result.capabilities ?? {});
  const capabilities = Object.fromEntries(
    requiredCapabilities.map((key) => [key, result.capabilities?.[key] === true]),
  );
  const errors = Array.isArray(result.errors) ? result.errors.map(String) : [];
  const passedCount = Object.values(capabilities).filter(Boolean).length;
  const passed = passedCount === requiredCapabilities.length && errors.length === 0;
  const metadata = Object.fromEntries(
    socialMetadataKeys
      .filter((key) => result.metadata?.[key] !== undefined)
      .map((key) => [key, result.metadata[key]]),
  );
  const evidence = {
    schemaVersion: 1,
    status: passed ? 'passed' : 'failed',
    capabilities,
    aggregate: {
      passed,
      requiredCount: requiredCapabilities.length,
      passedCount,
    },
    errors,
    metadata,
  };

  await mkdir(dirname(outputPath), { recursive: true });
  await writeFile(outputPath, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
}
