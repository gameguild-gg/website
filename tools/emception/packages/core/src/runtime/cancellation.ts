/**
 * Cancellation primitives.
 *
 * Runtime-agnostic helpers that wrap an in-flight tool invocation with a
 * `timeoutMs` deadline and/or an `AbortSignal`. The helper resolves with
 * a discriminated outcome so both Browser and Node adapters share the
 * same termination semantics:
 *
 *   - `kind: 'ok'`     → the underlying promise settled in time.
 *   - `kind: 'timeout'`→ `timeoutMs` elapsed first; cleanup ran.
 *   - `kind: 'abort'` → caller's `AbortSignal` fired first; cleanup ran.
 *
 * The caller supplies a `cleanup` callback (typically: terminate the WASM
 * tool instance / worker thread) which is invoked exactly once on the
 * losing path. The helper does NOT mutate the underlying promise — it
 * just stops waiting for it. Adapters are responsible for tearing the
 * tool down inside `cleanup` so the next `run()` gets a fresh instance.
 *
 * Lives in `@emception/core` so test harnesses, adapters, and the test
 * engine all dispatch on the same outcome shape; no DOM, no Node APIs.
 */

import { TimeoutError } from '../errors.js';

/** Discriminated outcome of `withCancellation`. */
export type CancellationOutcome<T> =
  | { kind: 'ok'; value: T; durationMs: number }
  | { kind: 'timeout'; durationMs: number; timeoutMs: number }
  | { kind: 'abort'; durationMs: number; reason: unknown };

export interface WithCancellationOptions {
  /** Finite elapsed-time deadline. `<=0` or `undefined` disables the timer. */
  timeoutMs?: number;
  /** Cooperative cancel from the caller. */
  signal?: AbortSignal;
  /**
   * Invoked exactly once when the deadline or signal wins the race.
   * Should terminate the tool instance / worker so the underlying
   * promise can no longer hold resources. Asynchronous cleanup is awaited
   * before resolving, with the cancellation outcome reserved beforehand.
   */
  cleanup?: () => void | Promise<void>;
  /**
   * Clock injection for tests. Defaults to the monotonic performance clock
   * where available, falling back to `Date.now`. Must return a
   * monotonically non-decreasing value in milliseconds.
   */
  now?: () => number;
}

/**
 * Race `op` against `timeoutMs` and `signal`. Returns a discriminated
 * outcome describing which side won. The outcome always carries
 * `durationMs` (elapsed time from entry to resolution).
 */
export async function withCancellation<T>(op: Promise<T>, opts: WithCancellationOptions = {}): Promise<CancellationOutcome<T>> {
  const { timeoutMs, signal, cleanup } = opts;
  if (timeoutMs !== undefined && !Number.isFinite(timeoutMs)) {
    throw new RangeError('timeoutMs must be finite.');
  }
  const now = opts.now ?? (() => globalThis.performance?.now() ?? Date.now());
  const start = now();

  if (signal?.aborted) {
    await safeCleanup(cleanup);
    return {
      kind: 'abort',
      durationMs: 0,
      reason: signal.reason,
    };
  }

  let timer: ReturnType<typeof setTimeout> | undefined;
  let abortHandler: (() => void) | undefined;

  try {
    return await new Promise<CancellationOutcome<T>>((resolve) => {
      let settled = false;
      const finish = (outcome: CancellationOutcome<T>) => {
        if (settled) return;
        settled = true;
        resolve(outcome);
      };

      op.then(
        (value) => finish({ kind: 'ok', value, durationMs: now() - start }),
        // Surface op rejections as a thrown error to keep symmetry with
        // a normal `await op` — adapters usually want stack-trace fidelity
        // when the tool crashes vs. when the deadline expired.
        (err) => {
          if (settled) return;
          settled = true;
          resolve(Promise.reject(err) as never);
        },
      );

      if (typeof timeoutMs === 'number' && timeoutMs > 0) {
        const checkDeadline = () => {
          if (settled) return;
          const remaining = timeoutMs - (now() - start);
          if (remaining > 0) {
            timer = setTimeout(checkDeadline, Math.min(remaining, 2_147_483_647));
            return;
          }
          settled = true;
          void safeCleanup(cleanup).then(() => {
            resolve({
              kind: 'timeout',
              durationMs: now() - start,
              timeoutMs,
            });
          });
        };
        timer = setTimeout(checkDeadline, Math.min(timeoutMs, 2_147_483_647));
      }

      if (signal) {
        abortHandler = () => {
          if (settled) return;
          settled = true;
          void safeCleanup(cleanup).then(() => {
            resolve({
              kind: 'abort',
              durationMs: now() - start,
              reason: signal.reason,
            });
          });
        };
        signal.addEventListener('abort', abortHandler, { once: true });
      }
    });
  } finally {
    if (timer !== undefined) clearTimeout(timer);
    if (signal && abortHandler) signal.removeEventListener('abort', abortHandler);
  }
}

/**
 * Convenience wrapper: throws `TimeoutError` on timeout, re-throws
 * `signal.reason` on abort, and returns the value on success. Useful
 * for callers that don't want to switch on the discriminated outcome.
 */
export async function withTimeoutOrThrow<T>(op: Promise<T>, opts: WithCancellationOptions = {}): Promise<T> {
  const outcome = await withCancellation(op, opts);
  switch (outcome.kind) {
    case 'ok':
      return outcome.value;
    case 'timeout':
      throw new TimeoutError(`Operation exceeded ${outcome.timeoutMs}ms (ran ${outcome.durationMs}ms before cleanup).`);
    case 'abort':
      // Preserve the caller's reason verbatim (DOMException, Error, string, …).
      throw outcome.reason ?? new Error('Operation aborted.');
  }
}

async function safeCleanup(cleanup?: () => void | Promise<void>): Promise<void> {
  if (!cleanup) return;
  try {
    await cleanup();
  } catch {
    // Cleanup failures are intentionally swallowed: the caller is already
    // on a losing path (timeout / abort) and we don't want to mask the
    // termination reason with a teardown error.
  }
}
