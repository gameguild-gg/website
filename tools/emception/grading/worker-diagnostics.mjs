const kinds = Object.freeze({
  input: 'invalid-request',
  'artifact verification': 'artifact-binding-failed',
  'runtime server': 'runtime-server-failed',
  'browser startup': 'browser-startup-failed',
  'WebAssembly evaluation': 'execution-failed',
});

/** Infrastructure diagnostics contain no paths, source, test names or raw errors. */
export function createWorkerFailureDiagnostic(phase, error) {
  const knownPhase = Object.hasOwn(kinds, phase) ? phase : 'unavailable';
  let kind = kinds[knownPhase] ?? 'unknown';
  if (knownPhase === 'browser startup' && error instanceof Error &&
      /No usable sandbox|SUID sandbox|Failed to move to new namespace|Operation not permitted|Running as root without/i.test(error.message)) {
    kind = 'browser-sandbox-unavailable';
  }
  return JSON.stringify({ schemaVersion: 1, phase: knownPhase, kind });
}
