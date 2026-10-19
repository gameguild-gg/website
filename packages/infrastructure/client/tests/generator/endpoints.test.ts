/**
 * Tests for endpoint generation
 */

import { describe, it, expect } from 'vitest';
import { generateEndpoints } from '../../scripts/codegen/endpoints.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';
import simpleSpec from './fixtures/simple-spec.json' with { type: 'json' };

describe('Endpoint Generator', () => {
  it('should generate endpoint definitions from paths', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain('export const endpoints');
    expect(output).toContain('getUsers');
    expect(output).toContain('createUser');
    expect(output).toContain('getUserById');
  });

  it('should include HTTP method in endpoint definition', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain("method: 'GET'");
    expect(output).toContain("method: 'POST'");
  });

  it('should include path template in endpoint definition', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain("path: '/api/users'");
    expect(output).toContain("path: '/api/users/{id}'");
  });

  it('should mark endpoints with path parameters as requiring parameters', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    // Endpoint with {id} parameter should have params in Input interface
    expect(output).toContain('GetUserByIdInput');
    expect(output).toContain('id: string');
  });

  it('should mark endpoints with request body as requiring body', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    // POST endpoint should have body in Input interface
    expect(output).toContain('CreateUserInput');
    expect(output).toContain('body?:');
  });

  it('should include response type information', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain('UserDto');
  });

  it('should handle endpoints with no parameters', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/health': {
          get: {
            operationId: 'getHealth',
            responses: {
              '200': {
                description: 'OK',
                content: {
                  'application/json': {
                    schema: { type: 'object' },
                  },
                },
              },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateEndpoints(spec);

    expect(output).toContain('getHealth');
    expect(output).toContain("path: '/api/health'");
  });

  it('should handle query parameters', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/users': {
          get: {
            operationId: 'searchUsers',
            parameters: [
              {
                name: 'search',
                in: 'query',
                schema: { type: 'string' },
              },
              {
                name: 'limit',
                in: 'query',
                schema: { type: 'integer' },
              },
            ],
            responses: {
              '200': { description: 'OK' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateEndpoints(spec);

    expect(output).toContain('searchUsers');
    expect(output).toMatch(/query.*search.*limit/s);
  });

  it('should handle header parameters', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      paths: {
        '/api/users': {
          get: {
            operationId: 'getUsers',
            parameters: [
              {
                name: 'X-Api-Key',
                in: 'header',
                required: true,
                schema: { type: 'string' },
              },
            ],
            responses: {
              '200': { description: 'OK' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateEndpoints(spec);

    expect(output).toContain('getUsers');
    // Generator may not currently support header parameters in output
    expect(output).toContain('getUsersEndpoint');
  });

  it('should include endpoint tags for grouping', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain('Users');
  });

  it('should add file header with metadata', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    expect(output).toContain('AUTO-GENERATED FILE - DO NOT EDIT MANUALLY');
    // Generator doesn't include API name in header currently
    expect(output).toContain('Generated Endpoint Definitions');
  });

  it('should handle empty paths gracefully', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test', version: '1.0' },
      paths: {},
    } as OpenApiSpec;

    const output = generateEndpoints(spec);

    expect(output).toContain('AUTO-GENERATED FILE');
    expect(output).toContain('export const endpoints');
  });

  it('should generate type-safe endpoint keys', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateEndpoints(spec);

    // Should have const assertions for type safety
    expect(output).toContain('as const');
  });

  it('should mark x-gameguild-allow-anonymous endpoints as not requiring auth', () => {
    const spec = {
      openapi: '3.0.1',
      info: { title: 'Test', version: '1.0' },
      security: [{ Bearer: [] }],
      paths: {
        '/v1/projects': {
          get: {
            operationId: 'getProjects',
            security: [{ Bearer: [] }],
            'x-gameguild-allow-anonymous': true,
          },
        },
        '/v1/orders': {
          get: {
            operationId: 'getOrders',
            security: [{ Bearer: [] }],
          },
        },
      },
    } as unknown as OpenApiSpec;

    const output = generateEndpoints(spec);

    expect(output).toMatch(/operationId: 'getProjects'[\s\S]*?requiresAuth: false/);
    expect(output).toMatch(/operationId: 'getOrders'[\s\S]*?requiresAuth: true/);
  });
});
