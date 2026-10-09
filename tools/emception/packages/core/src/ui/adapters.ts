// Shared kebab↔camel + DOM-event-name conventions.
//
// The webcomponent (`<emception-run kebab-attr>`) and the React component
// (`<EmceptionRun camelProp>`) both ultimately funnel into the same
// `normalizeViewConfig()` validator, but the *attribute* layer
// each consumes diverges:
//
//   - HTML attrs are kebab-case strings; booleans are presence-only or
//     "true"/"false"; nested values must be flattened.
//   - React props are camelCase JS values; booleans are real booleans;
//     nested objects pass through as-is.
//
// Centralizing the kebab↔camel conversion + the flat-attr → structured
// `ViewConfigInput` mapping here means both adapters consume one canonical
// schema. The same module also pins the DOM event-name registry
// (`emception-*`) so the webcomponent can derive CustomEvent names
// straight from the `EmceptionEventName` union without drift.
//
// Pure core: no DOM, no React, no Node. Inputs are plain string maps;
// outputs are plain JS objects.

import type { EmceptionEventName } from '../events.js';
import type { ViewConfigInput } from './config.js';

// ─────────────── kebab ⇄ camel ───────────────

/** `'foo-bar-baz'` → `'fooBarBaz'`. Idempotent on already-camel input. */
export function kebabToCamel(s: string): string {
  return s.replace(/-([a-z0-9])/g, (_, c: string) => c.toUpperCase());
}

/** `'fooBarBaz'` → `'foo-bar-baz'`. Idempotent on already-kebab input. */
export function camelToKebab(s: string): string {
  return s.replace(/[A-Z]/g, (c) => '-' + c.toLowerCase());
}

/**
 * Parse an HTML-attribute value into a boolean per the
 * "presence = true, absence = false, explicit `'false'` = false" rule.
 *
 * - `null`/`undefined`        → `false` (attr not set)
 * - `''` (empty string)       → `true`  (attr present, no value)
 * - `'false'`/`'0'`/`'no'`    → `false`
 * - anything else             → `true`
 */
export function parseBooleanAttr(value: string | null | undefined): boolean {
  if (value == null) return false;
  if (value === '') return true;
  const v = value.trim().toLowerCase();
  return v !== 'false' && v !== '0' && v !== 'no';
}

/** Parse a comma- or whitespace-separated list (e.g. `cflags="-O2 -Wall"`). */
export function parseListAttr(value: string | null | undefined): string[] | undefined {
  if (value == null) return undefined;
  const parts = value.split(/[\s,]+/).filter(Boolean);
  return parts.length > 0 ? parts : undefined;
}

// ─────────────── DOM event names ───────────────

/**
 * Stable mapping from internal `EmceptionEventName` (lowercase, possibly
 * hyphenated already) to the DOM `CustomEvent` name the webcomponent
 * dispatches. All names are prefixed with `'emception-'`.
 *
 * This is a value (not a type-only) so it can be enumerated at runtime by
 * the webcomponent to wire one DOM listener per event.
 */
export const EVENT_DOM_NAMES = {
  progress: 'emception-progress',
  ready: 'emception-ready',
  'bundle-loaded': 'emception-bundle-loaded',
  stdout: 'emception-stdout',
  stderr: 'emception-stderr',
  exit: 'emception-exit',
  'test-case': 'emception-test-case',
  'test-report': 'emception-test-report',
} as const satisfies Record<EmceptionEventName, string>;

export type EventDomName = (typeof EVENT_DOM_NAMES)[EmceptionEventName];

/** `domEventNameFor('test-case') === 'emception-test-case'`. */
export function domEventNameFor<E extends EmceptionEventName>(name: E): (typeof EVENT_DOM_NAMES)[E] {
  return EVENT_DOM_NAMES[name];
}

// ─────────────── flat attrs → ViewConfigInput ───────────────

/**
 * The full attribute schema both adapters honor. Each entry says how the
 * attribute name maps onto `ViewConfigInput` (or its nested `workspace.build`).
 *
 * Adding a new attribute means: add it here, and both the webcomponent +
 * React surfaces inherit support. No drift.
 */
type AttrKind = 'string' | 'boolean' | 'list' | 'enum';

interface AttrSpec {
  /** Where the parsed value lands. Dot-paths into ViewConfigInput. */
  target: string;
  kind: AttrKind;
  /** For `kind === 'enum'`, the allowed values (lowercased). */
  values?: readonly string[];
}

/**
 * Single source of truth for which attributes both adapters honor and
 * how each maps into `ViewConfigInput`. Webcomponent reads kebab keys
 * directly; React converts camel props via `camelToKebab` first.
 */
export const ATTRIBUTE_SCHEMA: Record<string, AttrSpec> = {
  // Top-level config.
  preset: { target: 'preset', kind: 'string' },
  'manifest-url': { target: 'manifestUrl', kind: 'string' },
  workspace: { target: 'workspace', kind: 'string' },
  source: { target: 'source', kind: 'string' },
  'seed-url': { target: 'seedUrl', kind: 'string' },
  'build-url': { target: 'buildUrl', kind: 'string' },
  'seed-policy': {
    target: 'seedPolicy',
    kind: 'enum',
    values: ['once', 'merge', 'overwrite'],
  },
  autorun: { target: 'autorun', kind: 'boolean' },
  canvas: { target: 'canvas', kind: 'boolean' },
  'show-hidden': { target: 'showHidden', kind: 'boolean' },
  'show-solution': { target: 'showSolution', kind: 'boolean' },

  // Build-config flatteners.
  output: { target: 'workspace.output', kind: 'string' },
  flags: { target: 'workspace.flags', kind: 'list' },
  ldflags: { target: 'workspace.ldflags', kind: 'list' },
  libs: { target: 'workspace.libs', kind: 'list' },
  'include-paths': { target: 'workspace.includePaths', kind: 'list' },
  'lib-paths': { target: 'workspace.libPaths', kind: 'list' },
};

export interface ParseAttributesOptions {
  /**
   * Called for any attribute name not in `ATTRIBUTE_SCHEMA`. Default is to
   * silently ignore (forward-compat with newer attrs); pass a thrower if
   * you want strict mode.
   */
  onUnknown?: (name: string, value: string) => void;
}

/**
 * Parse a flat string-attribute map (kebab keys) into a `ViewConfigInput`.
 *
 * - Booleans use `parseBooleanAttr` semantics.
 * - List attributes split on whitespace / commas.
 * - Enum attributes are validated case-insensitively; bad values throw.
 * - Build-related attributes are folded into `workspace` directly even when
 *   `workspace` itself isn't set (the validator will invent a name).
 *
 * Returns the partial `ViewConfigInput`; callers should hand it straight
 * to `normalizeViewConfig`.
 */
export function parseAttributesToInput(attrs: Record<string, string | null | undefined>, opts: ParseAttributesOptions = {}): ViewConfigInput {
  const out: Record<string, unknown> = {};

  for (const [rawName, value] of Object.entries(attrs)) {
    if (value === undefined) continue;
    // `Object.hasOwn` blocks lookups for keys like 'constructor' or
    // 'toString' that would otherwise resolve to Object.prototype
    // members and look like valid schema entries.
    const spec = Object.hasOwn(ATTRIBUTE_SCHEMA, rawName) ? ATTRIBUTE_SCHEMA[rawName] : undefined;
    if (!spec) {
      opts.onUnknown?.(rawName, value ?? '');
      continue;
    }

    const parsed = parseValue(rawName, value, spec);
    if (parsed === undefined) continue;
    setPath(out, spec.target, parsed);
  }

  return out as ViewConfigInput;
}

// ─────────────── helpers ───────────────

function parseValue(name: string, value: string | null, spec: AttrSpec): unknown | undefined {
  switch (spec.kind) {
    case 'boolean':
      return parseBooleanAttr(value);
    case 'list':
      return parseListAttr(value);
    case 'string':
      return value === null || value === '' ? undefined : value;
    case 'enum': {
      if (value === null || value === '') return undefined;
      const lower = value.toLowerCase();
      if (!spec.values || !spec.values.includes(lower)) {
        throw new RangeError(`Attribute '${name}': expected one of ${spec.values?.join(', ') ?? '(none)'}, got '${value}'.`);
      }
      return lower;
    }
  }
}

/**
 * Set `obj[a.b.c] = value`, creating intermediate plain objects. Rejects
 * `__proto__` / `prototype` / `constructor` path segments so crafted paths
 * cannot pollute Object.prototype (schema gating is the first line of
 * defense; this is the hard backstop).
 *
 * Defense in depth, layer by layer:
 *   - reserved segment blocklist (`__proto__` / `prototype` / `constructor`);
 *   - own-property-only traversal (`Object.hasOwn`), so inherited members
 *     can never be confused for existing path segments;
 *   - the final write uses `Object.defineProperty`, which never invokes
 *     setters (the `__proto__` poison pill) the way `obj[key] = v` can.
 */
function setPath(obj: Record<string, unknown>, dotted: string, value: unknown): void {
  const parts = dotted.split('.');
  let cur: Record<string, unknown> = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    const key = parts[i]!;
    if (key === '__proto__' || key === 'prototype' || key === 'constructor') return;
    const next = Object.hasOwn(cur, key) ? cur[key] : undefined;
    if (next == null || typeof next !== 'object') {
      const fresh: Record<string, unknown> = {};
      cur[key] = fresh;
      cur = fresh;
    } else {
      cur = next as Record<string, unknown>;
    }
  }
  const last = parts[parts.length - 1]!;
  if (last === '__proto__' || last === 'prototype' || last === 'constructor') return;
  Object.defineProperty(cur, last, { value, writable: true, enumerable: true, configurable: true });
}
