import { describe, expect, it } from 'vitest';

import { generateModules } from '../../scripts/codegen/modules.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';

describe('Module Generator', () => {
  it.each([
    { label: 'explicit anonymous endpoint', marker: true, security: undefined, requiresAuth: false },
    { label: 'explicit protected endpoint', marker: false, security: undefined, requiresAuth: true },
    { label: 'non-boolean anonymous marker', marker: 'true', security: undefined, requiresAuth: true },
    { label: 'unmarked endpoint', marker: undefined, security: undefined, requiresAuth: true },
    { label: 'explicit empty security', marker: undefined, security: [], requiresAuth: false },
  ])('preserves the native authentication decision for $label', ({ marker, security, requiresAuth }) => {
    const spec = {
      openapi: '3.0.1',
      info: { title: 'Test API', version: '1.0.0' },
      security: [{ Bearer: [] }],
      paths: {
        '/v1/auth/mfa/sign-in/enrollment': {
          post: {
            operationId: 'postAuthMfaSignInEnrollment',
            tags: ['Auth'],
            'x-gameguild-allow-anonymous': marker,
            security,
            responses: { '200': { description: 'OK' } },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateModules(spec)['auth'];

    expect(output).toContain(`requiresAuth: ${requiresAuth},`);
  });

  it.each([
    { label: 'named DTOs', items: { $ref: '#/components/schemas/User' }, expectedType: 'Types.User' },
    { label: 'unstructured objects', items: { type: 'object' }, expectedType: 'Record<string, unknown>' },
  ])('reproduces equivalent shorthand array response types for $label', ({ items, expectedType }) => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test API', version: '1.0.0' },
      paths: {
        '/users': {
          get: {
            operationId: 'getUsers',
            tags: ['Users'],
            responses: {
              '200': {
                description: 'OK',
                content: {
                  'application/json': {
                    schema: { type: 'array', items },
                  },
                },
              },
            },
          },
        },
      },
      components: { schemas: { User: { type: 'object', properties: { id: { type: 'string' } } } } },
    } as OpenApiSpec;

    const output = generateModules(spec)['users'];

    expect(output).toContain(`Promise<Result<${expectedType}[], ApiError>>`);
    expect(output).toContain(`return result as Result<${expectedType}[], ApiError>;`);
  });

  it('preserves word boundaries from PascalCase OpenAPI tags in public module names', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test API', version: '1.0.0' },
      paths: {
        '/v1/subscriptions': {
          get: {
            operationId: 'getSubscriptions',
            tags: ['CommerceSubscriptions'],
            responses: { '200': { description: 'OK' } },
          },
        },
      },
    } as OpenApiSpec;

    const modules = generateModules(spec);

    expect(modules['commerce-subscriptions']).toContain('export class CommerceSubscriptionsModule');
  });

  it('emits required body parameters before optional query parameters', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test API', version: '1.0.0' },
      paths: {
        '/v1/courses/{courseId}/interactions/progress': {
          put: {
            operationId: 'putCourseInteractionsProgress',
            tags: ['Learning/courses/contentInteraction'],
            parameters: [
              {
                name: 'courseId',
                in: 'path',
                required: true,
                schema: { type: 'string' },
              },
              {
                name: 'includeHistory',
                in: 'query',
                required: false,
                schema: { type: 'boolean' },
              },
            ],
            requestBody: {
              required: true,
              content: {
                'application/json': {
                  schema: {
                    $ref: '#/components/schemas/UpdateProgressInput',
                  },
                },
              },
            },
            responses: {
              '200': {
                description: 'Updated',
                content: {
                  'application/json': {
                    schema: {
                      $ref: '#/components/schemas/ProgressResult',
                    },
                  },
                },
              },
            },
          },
        },
      },
      components: {
        schemas: {
          UpdateProgressInput: {
            type: 'object',
            properties: {
              progress: { type: 'number' },
            },
          },
          ProgressResult: {
            type: 'object',
            properties: {
              success: { type: 'boolean' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const modules = generateModules(spec);
    const output = modules['learning-courses-content-interaction'];

    expect(output).toBeDefined();
    expect(output).toContain(
      'async putCourseInteractionsProgress(courseId: string, body: Types.UpdateProgressInput, query?: { includeHistory?: boolean })',
    );
  });
});
