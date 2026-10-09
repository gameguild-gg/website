import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';

import { afterAll, describe, expect, it } from 'vitest';

import { safeJoin } from '../../scripts/utils/safe-join.js';

const tempDirs: string[] = [];

function makeBase(): string {
  const dir = mkdtempSync(join(tmpdir(), 'safe-join-'));
  tempDirs.push(dir);
  return dir;
}

afterAll(() => {
  for (const dir of tempDirs) rmSync(dir, { recursive: true, force: true });
});

describe('safeJoin', () => {
  it('allows a relative subpath inside the base directory', () => {
    const base = makeBase();
    expect(safeJoin(base, 'sub/file.ts')).toBe(join(base, 'sub', 'file.ts'));
  });

  it('allows nested relative subpaths', () => {
    const base = makeBase();
    expect(safeJoin(base, 'modules/Commerce-Payments.gen.ts')).toBe(join(base, 'modules', 'Commerce-Payments.gen.ts'));
  });

  it('allows the base directory itself', () => {
    const base = makeBase();
    expect(safeJoin(base, '.')).toBe(base);
  });

  it("rejects '..'-escapes out of the base", () => {
    const base = makeBase();
    expect(() => safeJoin(base, '../etc/passwd')).toThrow(/escaping base directory/);
  });

  it('rejects a nested ..-escape that leaves the base', () => {
    const base = makeBase();
    expect(() => safeJoin(base, 'sub/../../elsewhere')).toThrow(/escaping base directory/);
  });

  it('rejects absolute paths', () => {
    const base = makeBase();
    expect(() => safeJoin(base, '/etc/passwd')).toThrow(/absolute path/);
  });

  it('rejects NUL bytes', () => {
    const base = makeBase();
    expect(() => safeJoin(base, 'sub\0evil')).toThrow(/NUL/);
  });

  it('does not accept a sibling directory sharing the base as a string prefix', () => {
    const base = makeBase();
    expect(() => safeJoin(base, `../${basename(base)}-evil/x`)).toThrow(/escaping base directory/);
  });
});
