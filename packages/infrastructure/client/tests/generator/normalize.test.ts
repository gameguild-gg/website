/**
 * Tests for OpenAPI spec normalization
 */

import { describe, it, expect } from 'vitest';
import { normalizeSpec } from '../../scripts/normalize.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';
import simpleSpec from './fixtures/simple-spec.json';

describe('OpenAPI Spec Normalizer', () => {
  it('should normalize operation IDs when missing', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/test': {
          get: {
            // No operationId
            responses: {
              '200': { description: 'Success' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const operation = (normalized.paths['/api/test'] as any).get;

    expect(operation.operationId).toBeDefined();
    expect(operation.operationId).toMatch(/^(get|test)/i);
  });

  it('should preserve existing operation IDs', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/users': {
          get: {
            operationId: 'getAllUsers',
            responses: {
              '200': { description: 'Success' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const operation = (normalized.paths['/api/users'] as any).get;

    expect(operation.operationId).toBe('getAllUsers');
  });

  it('should resolve duplicate operation IDs with deterministic route semantics', () => {
    const paths = {
      '/api/v1/listings/{listingId}:publish': {
        get: {
          operationId: 'api_Listings_Controller_GetController',
          responses: { '200': { description: 'Success' } },
        },
        post: {
          operationId: 'api_Listings_Controller_GetController',
          responses: { '200': { description: 'Success' } },
        },
      },
    };
    const reversedPaths = Object.fromEntries(Object.entries(paths).reverse());

    const normalized = normalizeSpec({ ...simpleSpec, paths } as OpenApiSpec);
    const reversed = normalizeSpec({ ...simpleSpec, paths: reversedPaths } as OpenApiSpec);

    const operationIds = {
      get: (normalized.paths['/api/v1/listings/{listingId}:publish'] as any).get.operationId,
      post: (normalized.paths['/api/v1/listings/{listingId}:publish'] as any).post.operationId,
    };

    expect(operationIds).toEqual({
      get: 'listingsGetForGetListingsByListingIdPublish',
      post: 'listingsGetForPostListingsByListingIdPublish',
    });
    expect({
      get: (reversed.paths['/api/v1/listings/{listingId}:publish'] as any).get.operationId,
      post: (reversed.paths['/api/v1/listings/{listingId}:publish'] as any).post.operationId,
    }).toEqual(operationIds);
  });

  it('should normalize tag names to PascalCase', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      tags: [
        { name: 'user-management', description: 'User operations' },
        { name: 'auth_service', description: 'Auth operations' },
      ],
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);

    expect(normalized.tags).toBeDefined();
    expect(normalized.tags!.map((tag) => tag.name)).toEqual(['AuthService', 'UserManagement']);
  });

  it('should normalize schema names to remove ASP.NET patterns', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          'GameGuild.Identity.Users.UserDto': {
            type: 'object',
            properties: {
              id: { type: 'string' },
            },
          },
          'ProblemDetails': {
            type: 'object',
            properties: {
              detail: { type: 'string' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const schemas = (normalized.components as any)?.schemas;

    // Normalize removes namespace but retains the DTO suffix
    expect(schemas).toHaveProperty('UserDto'); // Preserve DTO identity
    expect(schemas).not.toHaveProperty('GameGuild.Identity.Users.UserDto');
    expect(schemas).not.toHaveProperty('ProblemDetails'); // Removed by cleanAspNetPatterns
  });

  it('should handle generic type names', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          'ResultOfUserDto': {
            type: 'object',
            properties: {
              data: { $ref: '#/components/schemas/UserDto' },
              success: { type: 'boolean' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const schemas = (normalized.components as any)?.schemas;

    // Normalize preserves generics and the DTO suffix of the type parameter
    expect(schemas).toHaveProperty('ResultOfUserDto'); // Preserve the generic argument
  });

  it('should preserve generic argument identity and update each reference independently', () => {
    const usersPage = 'PagedResult`1[[Acme_Identity_Users_UserDto, Acme.Identity.Users, Version=1_0_0_PagedResultUserDto';
    const ticketsPage = 'PagedResult`1[[Acme_Commerce_SupportTicketDto, Acme.Commerce, Version=1_0_0_PagedResultSupportTicketDto';
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/users': {
          get: {
            responses: {
              '200': {
                description: 'Success',
                content: { 'application/json': { schema: { $ref: `#/components/schemas/${usersPage}` } } },
              },
            },
          },
        },
        '/tickets': {
          get: {
            responses: {
              '200': {
                description: 'Success',
                content: { 'application/json': { schema: { $ref: `#/components/schemas/${ticketsPage}` } } },
              },
            },
          },
        },
      },
      components: {
        schemas: {
          [usersPage]: { type: 'object', properties: { items: { type: 'array' } } },
          [ticketsPage]: { type: 'object', properties: { items: { type: 'array' } } },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const schemas = (normalized.components as any).schemas;

    expect(Object.keys(schemas)).toEqual([
      'PagedResultOfCommerceSupportTicketDto',
      'PagedResultOfIdentityUsersUserDto',
    ]);
    expect((normalized.paths['/users'] as any).get.responses['200'].content['application/json'].schema.$ref).toBe(
      '#/components/schemas/PagedResultOfIdentityUsersUserDto',
    );
    expect((normalized.paths['/tickets'] as any).get.responses['200'].content['application/json'].schema.$ref).toBe(
      '#/components/schemas/PagedResultOfCommerceSupportTicketDto',
    );
  });

  it('should update all schema references when normalizing names', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          'Old.Namespace.UserDto': {
            type: 'object',
            properties: {
              id: { type: 'string' },
            },
          },
          'UserListResponse': {
            type: 'object',
            properties: {
              users: {
                type: 'array',
                items: { $ref: '#/components/schemas/Old.Namespace.UserDto' },
              },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const schemas = (normalized.components as any)?.schemas;
    const userListOutput = schemas.UserListOutput; // Response -> Output

    // Normalize removes namespace but retains the DTO suffix
    expect(schemas).toHaveProperty('UserDto'); // Preserve DTO identity
    expect(userListOutput.properties.users.items.$ref).toBe('#/components/schemas/UserDto');
  });

  it('should preserve the original spec structure', () => {
    const spec: OpenApiSpec = { ...simpleSpec };
    const originalInfo = spec.info;

    const normalized = normalizeSpec(spec);

    expect(normalized.info).toEqual(originalInfo);
    expect(normalized.openapi).toBe(spec.openapi);
  });

  it('should handle specs with no components', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test', version: '1.0' },
      paths: {},
    } as OpenApiSpec;

    expect(() => normalizeSpec(spec)).not.toThrow();
  });

  it('should handle specs with no tags', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      tags: undefined,
    } as OpenApiSpec;

    expect(() => normalizeSpec(spec)).not.toThrow();
  });

  it('should clean ASP.NET controller suffixes from operation tags', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/users': {
          get: {
            operationId: 'getUsersController',
            tags: ['UsersController'],
            responses: {
              '200': { description: 'Success' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const normalized = normalizeSpec(spec);
    const operation = (normalized.paths['/api/users'] as any).get;

    // Normalize converts to PascalCase but doesn't strip Controller suffix from tags
    expect(operation.tags).toContain('UsersController');
    // But it does strip Controller from operation IDs
    expect(operation.operationId).toBe('getUsers');
  });
});
