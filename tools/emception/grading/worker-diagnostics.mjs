/** @file Project worker failures into phase and kind labels without raw error details. */

const kinds = Object.freeze({
  input: 'invalid-request',
  'artifact verification': 'artifact-binding-failed',
  'runtime server': 'runtime-server-failed',
  'browser startup': 'browser-startup-failed',
  'WebAssembly evaluation': 'execution-failed',
});

const isSandboxFailure = (error) => error instanceof Error &&
  /No usable sandbox|SUID sandbox|Failed to move to new namespace|Operation not permitted|Running as root without/i.test(error.message);

const resolveFailureKind = (phase, error) =>
  phase === 'browser startup' && isSandboxFailure(error)
    ? 'browser-sandbox-unavailable'
    : kinds[phase] ?? 'unknown';

/**
 * Infrastructure diagnostics contain no paths, source, test names or raw errors.
 * @param {string} phase - Current worker phase; unrecognized values become "unavailable".
 * @param {unknown} error - Failure used only to classify a browser sandbox error.
 * @returns {string} Serialized schema version, recognized phase and failure kind.
 * @example
 * const diagnostic = createWorkerFailureDiagnostic('input', undefined);
 */
export const createWorkerFailureDiagnostic = (phase, error) => {
  const knownPhase = Object.hasOwn(kinds, phase) ? phase : 'unavailable';
  return JSON.stringify({ schemaVersion: 1, phase: knownPhase, kind: resolveFailureKind(knownPhase, error) });
};
