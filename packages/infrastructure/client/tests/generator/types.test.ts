/**
 * Tests for TypeScript type generation
 */

import { describe, it, expect } from 'vitest';
import { generateTypes } from '../../scripts/codegen/types.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';
import simpleSpec from './fixtures/simple-spec.json';

describe('Type Generator', () => {
  it('should generate interface for object schema', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            required: ['id', 'email'],
            properties: {
              id: { type: 'string' },
              email: { type: 'string' },
              name: { type: 'string' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export interface UserDto');
    expect(output).toContain('id: string');
    expect(output).toContain('email: string');
    expect(output).toContain('name?: string'); // Optional because not in required array
  });

  it('should generate collision-safe legacy aliases for DTO schemas', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            properties: {
              id: { type: 'string' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export type User = UserDto;');
    expect(output).toContain('export { UserDtoSchema as UserSchema };');
  });

  it('should not generate a legacy DTO alias when the entity name already exists', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          User: {
            type: 'object',
            properties: {
              passwordHash: { type: 'string' },
            },
          },
          UserDto: {
            type: 'object',
            properties: {
              id: { type: 'string' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).not.toContain('export type User = UserDto;');
    expect(output).not.toContain('export { UserDtoSchema as UserSchema };');
  });

  it('should generate enum for string enum schema', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserRole: {
            type: 'string',
            enum: ['Admin', 'User', 'Guest'],
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    // Generator outputs union type instead of enum
    expect(output).toContain('export type UserRole');
    expect(output).toContain('Admin');
    expect(output).toContain('User');
    expect(output).toContain('Guest');
  });

  it('should generate type for primitive schema', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserId: {
            type: 'string',
            format: 'uuid',
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export type UserId = string');
  });

  it('should generate type for array schema', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          StringArray: {
            type: 'array',
            items: { type: 'string' },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    // Generator outputs Array<T> instead of T[]
    expect(output).toContain('export type StringArray = Array<string>');
  });

  it('should handle nested object schemas', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            properties: {
              id: { type: 'string' },
              profile: {
                type: 'object',
                properties: {
                  bio: { type: 'string' },
                  avatar: { type: 'string' },
                },
              },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export interface UserDto');
    expect(output).toContain('profile?:');
    expect(output).toContain('bio?: string');
    expect(output).toContain('avatar?: string');
  });

  it('should handle schema references', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            properties: {
              id: { type: 'string' },
              role: { $ref: '#/components/schemas/UserRole' },
            },
          },
          UserRole: {
            type: 'string',
            enum: ['Admin', 'User'],
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export interface UserDto');
    expect(output).toContain('role?: UserRole');
    // Generator outputs union type instead of enum
    expect(output).toContain('export type UserRole');
  });

  it('should handle union types (oneOf)', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          Response: {
            oneOf: [{ $ref: '#/components/schemas/SuccessResponse' }, { $ref: '#/components/schemas/ErrorResponse' }],
          },
          SuccessResponse: {
            type: 'object',
            properties: {
              data: { type: 'string' },
            },
          },
          ErrorResponse: {
            type: 'object',
            properties: {
              error: { type: 'string' },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('export type Response = SuccessResponse | ErrorResponse');
  });

  it('should handle allOf (composition)', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          BaseEntity: {
            type: 'object',
            properties: {
              id: { type: 'string' },
            },
          },
          UserDto: {
            allOf: [
              { $ref: '#/components/schemas/BaseEntity' },
              {
                type: 'object',
                properties: {
                  email: { type: 'string' },
                },
              },
            ],
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    // Generator outputs intersection type instead of extends
    expect(output).toContain('export type UserDto = BaseEntity &');
    expect(output).toContain('email?: string');
  });

  it('should handle nullable types', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            properties: {
              email: { type: 'string', nullable: true },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('email?: string | null');
  });

  it('should add JSDoc comments from schema descriptions', () => {
    const spec: OpenApiSpec = {
      ...simpleSpec,
      components: {
        schemas: {
          UserDto: {
            type: 'object',
            description: 'Represents a user in the system',
            properties: {
              id: {
                type: 'string',
                description: 'Unique identifier',
              },
            },
          },
        },
      },
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('/**');
    expect(output).toContain('Represents a user in the system');
    expect(output).toContain('Unique identifier');
  });

  it('should include file header with generation metadata', () => {
    const spec = simpleSpec as OpenApiSpec;
    const output = generateTypes(spec);

    expect(output).toContain('@game-guild/client - Generated Types');
    expect(output).toContain('AUTO-GENERATED FILE - DO NOT EDIT MANUALLY');
    expect(output).toContain('Generated from: Test API');
  });

  it('should handle empty components gracefully', () => {
    const spec: OpenApiSpec = {
      openapi: '3.0.1',
      info: { title: 'Test', version: '1.0' },
      paths: {},
    } as OpenApiSpec;

    const output = generateTypes(spec);

    expect(output).toContain('AUTO-GENERATED FILE');
    expect(output).not.toContain('export interface');
  });
});
