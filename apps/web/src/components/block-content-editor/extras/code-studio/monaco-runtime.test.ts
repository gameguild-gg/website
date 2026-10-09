// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { getTypeScriptSupport } from './monaco-runtime';

type Runtime = Exclude<Parameters<typeof getTypeScriptSupport>[0], null>;

describe('Monaco TypeScript namespace compatibility', () => {
  it('resolves the current top-level namespace', () => {
    const support = {} as NonNullable<Runtime['typescript']>;
    const runtime = { typescript: support, languages: {} } as Runtime;
    expect(getTypeScriptSupport(runtime)).toBe(support);
  });

  it('resolves the legacy browser-loader namespace', () => {
    const support = {} as NonNullable<Runtime['typescript']>;
    const runtime = { languages: { typescript: support } } as Runtime;
    expect(getTypeScriptSupport(runtime)).toBe(support);
  });

  it('tolerates an absent runtime or language support', () => {
    expect(getTypeScriptSupport(null)).toBeUndefined();
    expect(getTypeScriptSupport({ languages: {} } as Runtime)).toBeUndefined();
  });
});
