import { isAbsolute, resolve, sep } from 'node:path';

/**
 * Join an untrusted path segment onto a trusted base directory, rejecting any
 * segment that escapes the base (path traversal).
 *
 * Guarantees:
 * - `userPath` must be relative (absolute paths are rejected);
 * - the resolved result must stay inside `base` (segment-prefix match, so
 *   `/base-dir` vs `/base` cannot pass as a prefix of each other);
 * - NUL bytes are rejected (they truncate paths on some platforms/FS layers).
 *
 * Throws an Error describing the violation; never returns a path outside base.
 */
export function safeJoin(base: string, userPath: string): string {
  if (userPath.includes('\0')) {
    throw new Error(`Rejected path containing NUL byte: ${JSON.stringify(userPath)}`);
  }
  if (isAbsolute(userPath)) {
    throw new Error(`Rejected absolute path: ${JSON.stringify(userPath)}`);
  }

  const resolved = resolve(base, userPath);
  const normalizedBase = resolve(base);
  const baseWithSep = normalizedBase.endsWith(sep) ? normalizedBase : normalizedBase + sep;

  if (resolved !== normalizedBase && !resolved.startsWith(baseWithSep)) {
    throw new Error(`Rejected path escaping base directory ${JSON.stringify(normalizedBase)}: ${JSON.stringify(userPath)}`);
  }

  return resolved;
}
