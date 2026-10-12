import { mkdtempSync, rmSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const prettier = vi.hoisted(() => ({
  format: vi.fn(),
  resolveConfig: vi.fn(),
}));

vi.mock('prettier', () => ({
  format: prettier.format,
  resolveConfig: prettier.resolveConfig,
}));

import { formatOutput } from '../../scripts/utils/formatting.js';
import {
  capitalize,
  pluralize,
  sanitizeIdentifier,
  singularize,
  toCamelCase,
  toKebabCase,
  toPascalCase,
  toScreamingSnakeCase,
} from '../../scripts/utils/naming.js';
import { qualifyType } from '../../scripts/utils/type-qualify.js';

describe('generator utility helpers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('normalizes names across casing, identifiers, and word forms', () => {
    expect(toPascalCase('content-sync module')).toBe('ContentSyncModule');
    expect(toPascalCase('real-estate/listings')).toBe('RealEstateListings');
    expect(toPascalCase('content-')).toBe('Content');
    expect(toCamelCase('Template_name')).toBe('templateName');
    expect(capitalize('SCIENCE')).toBe('Science');
    expect(capitalize('')).toBe('');
    expect(toKebabCase('ContentSyncModule')).toBe('content-sync-module');
    expect(toKebabCase('content sync_module')).toBe('content-sync-module');
    expect(toScreamingSnakeCase('ContentSyncModule')).toBe('CONTENT_SYNC_MODULE');
    expect(toScreamingSnakeCase('content sync-module')).toBe('CONTENT_SYNC_MODULE');
    expect(sanitizeIdentifier('content.graph')).toBe('content_graph');
    expect(sanitizeIdentifier('123module')).toBe('_123module');
    expect(sanitizeIdentifier('class')).toBe('_class');
    expect(sanitizeIdentifier('valid_$Name')).toBe('valid_$Name');
    expect(pluralize('category')).toBe('categories');
    expect(pluralize('bus')).toBe('buses');
    expect(pluralize('match')).toBe('matches');
    expect(pluralize('brush')).toBe('brushes');
    expect(pluralize('template')).toBe('templates');
    expect(singularize('categories')).toBe('category');
    expect(singularize('classes')).toBe('class');
    expect(singularize('boxes')).toBe('box');
    expect(singularize('matches')).toBe('match');
    expect(singularize('brushes')).toBe('brush');
    expect(singularize('templates')).toBe('template');
    expect(singularize('glass')).toBe('glass');
    expect(singularize('content')).toBe('content');
  });

  it('qualifies model names without corrupting literals or built-in types', () => {
    expect(qualifyType("'draft' | 'published' | 'archived'")).toBe("'draft' | 'published' | 'archived'");
    expect(qualifyType('1 | 2 | true | false')).toBe('1 | 2 | true | false');
    expect(qualifyType('User | null')).toBe('Types.User | null');
    expect(qualifyType('{ id: string }')).toBe('{ id: string }');
    expect(qualifyType('(value: string) => void')).toBe('(value: string) => void');
    expect(qualifyType('Record<string, User>')).toBe('Record<string, User>');
    expect(qualifyType('Types.User')).toBe('Types.User');
    expect(qualifyType('Errors.ApiError')).toBe('Errors.ApiError');
    expect(qualifyType('Array<User>[]', 'Models')).toBe('Array<Models.User>[]');
    expect(qualifyType('Custom')).toBe('Types.Custom');
  });

  it('qualifies bracket arrays without treating primitive items as schemas', () => {
    expect(qualifyType('string[]')).toBe('string[]');
    expect(qualifyType('number[][]')).toBe('number[][]');
    expect(qualifyType('User[]')).toBe('Types.User[]');
    expect(qualifyType('User[] | null')).toBe('Types.User[] | null');
    expect(qualifyType('(User | null)[]')).toBe('(Types.User | null)[]');
    expect(qualifyType('(User & Profile)[]', 'Models')).toBe('(Models.User & Models.Profile)[]');
    expect(qualifyType("('draft' | 'published')[]")).toBe("('draft' | 'published')[]");
  });

  it('formats generated TypeScript with Prettier config', async () => {
    prettier.resolveConfig.mockResolvedValue({ semi: false });
    prettier.format.mockResolvedValue('const value = 1\n');

    await expect(formatOutput('const value=1', 'generated.ts')).resolves.toBe('const value = 1\n');
    expect(prettier.format).toHaveBeenCalledWith('const value=1', {
      semi: false,
      filepath: 'generated.ts',
      parser: 'typescript',
    });
  });

  it('returns unformatted code when Prettier fails', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    prettier.resolveConfig.mockRejectedValue(new Error('no config'));

    await expect(formatOutput('const value=1', 'generated.ts')).resolves.toBe('const value=1');
    expect(warn).toHaveBeenCalled();

    warn.mockRestore();
  });

  it('accepts absolute temporary file paths for formatting lookups', async () => {
    const dir = mkdtempSync(join(tmpdir(), 'modu-client-format-'));
    const filepath = join(dir, 'generated.ts');
    prettier.resolveConfig.mockResolvedValue(null);
    prettier.format.mockResolvedValue('export const ok = true;\n');

    await expect(formatOutput('export const ok=true', filepath)).resolves.toBe('export const ok = true;\n');
    expect(prettier.format).toHaveBeenCalledWith('export const ok=true', {
      filepath,
      parser: 'typescript',
    });

    rmSync(dir, { recursive: true, force: true });
  });
});
