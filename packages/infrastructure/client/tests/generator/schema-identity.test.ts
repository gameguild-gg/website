import { describe, expect, it } from 'vitest';
import { normalizeSpec, resolveKnownSchemaTypeName } from '../../scripts/normalize.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';

describe('stable shared schema identity', () => {
  it('retains the DTO name whether a product exposes the underlying entity or not', () => {
    const spec = (includeEntity: boolean) => normalizeSpec({
      openapi: '3.0.1', info: { title: 'Test', version: '1' },
      paths: { '/users': { get: { responses: { '200': {
        description: 'Success', content: { 'application/json': { schema: { $ref: '#/components/schemas/Identity_Users_UserDto' } } },
      } } } } },
      components: { schemas: {
        Identity_Users_UserDto: { type: 'object', properties: { name: { type: 'string' } } },
        ...(includeEntity ? { Identity_Users_User: { type: 'object', properties: { passwordHash: { type: 'string' } } } } : {}),
      } },
    } as OpenApiSpec);
    const alone = spec(false);
    const alongside = spec(true);
    expect(alone.paths).toEqual(alongside.paths);
    expect((alone.components as any).schemas.IdentityUsersUserDto).toEqual((alongside.components as any).schemas.IdentityUsersUserDto);
    expect((alone.components as any).schemas.IdentityUsersUserDto.properties).not.toHaveProperty('passwordHash');
  });

  it('resolves an exact DTO reference before a similarly named entity', () => {
    expect(resolveKnownSchemaTypeName('IdentityUsersUserDto', new Set(['IdentityUsersUser', 'IdentityUsersUserDto'])))
      .toBe('IdentityUsersUserDto');
  });
});
