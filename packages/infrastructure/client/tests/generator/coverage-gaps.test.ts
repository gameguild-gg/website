import { describe, expect, it } from 'vitest';

import { generateEndpoints } from '../../scripts/codegen/endpoints.js';
import { generateModules } from '../../scripts/codegen/modules.js';
import { generateTypes } from '../../scripts/codegen/types.js';
import {
  normalizeSchemaName,
  normalizeSpec,
  resolveKnownSchemaTypeName,
  toSchemaTypeName,
} from '../../scripts/normalize.js';
import type { OpenApiSpec } from '../../scripts/fetch-spec.js';
import type { OpenAPIV3 } from 'openapi-types';

function baseSpec(overrides: Partial<OpenApiSpec> = {}): OpenApiSpec {
  return {
    openapi: '3.0.1',
    info: { title: 'Coverage API', version: '1.0.0' },
    paths: {},
    components: { schemas: {} },
    ...overrides,
  } as OpenApiSpec;
}

function specWithoutPaths(): OpenApiSpec {
  const spec: OpenAPIV3.Document = {
    openapi: '3.0.1', info: { title: 'No Paths', version: '1.0.0' }, paths: {},
  };
  // Keep the deliberately malformed input used to exercise missing-path handling.
  Reflect.deleteProperty(spec, 'paths');
  return spec;
}

function operationWithoutResponses(fields: Omit<OpenAPIV3.OperationObject, 'responses'>): OpenAPIV3.OperationObject {
  const operation: OpenAPIV3.OperationObject = { ...fields, responses: {} };
  // Missing responses are an intentional robustness case, not a valid OpenAPI operation.
  Reflect.deleteProperty(operation, 'responses');
  return operation;
}

describe('generator coverage edge cases', () => {
  it('retains intentionally omitted fields in malformed document and operation fixtures', () => {
    expect(specWithoutPaths()).toEqual({ openapi: '3.0.1', info: { title: 'No Paths', version: '1.0.0' } });
    expect(operationWithoutResponses({ operationId: 'getNoResponses' })).toEqual({ operationId: 'getNoResponses' });
    expect(operationWithoutResponses({ tags: ['RealEstate/Listings'] })).toEqual({ tags: ['RealEstate/Listings'] });
  });

  it('normalizes duplicate operation ids, root paths, schema aliases, and conflicts', () => {
    const nestedSchema = {
      type: 'object',
      properties: {
        child: { $ref: '#/components/schemas/ExistingDto' },
        values: {},
        external: { $ref: '#/components/parameters/ExistingDto' },
        raw: {},
      },
    } satisfies OpenAPIV3.SchemaObject;
    // The recursive normalizer must also tolerate malformed array and null properties.
    Reflect.set(nestedSchema.properties, 'values', [{ $ref: '#/components/schemas/ExistingDto' }]);
    Reflect.set(nestedSchema.properties, 'raw', null);
    const normalized = normalizeSpec(
      baseSpec({
        tags: [{ name: 'real-estate/listings' }],
        paths: {
          '/empty': undefined,
          '/': {
            get: { tags: ['Root'], responses: {} },
            post: { responses: {} },
          },
          '/api/v1/listings/{listingId}:publish': {
            get: { operationId: 'api_Listings_Controller_GetController', tags: ['real-estate/listings'], responses: {} },
            post: { operationId: 'api_Listings_Controller_GetController', tags: ['real-estate/listings'], responses: {} },
          },
        },
        components: {
          schemas: {
            ExistingDto: { type: 'object', properties: { id: { type: 'string' } } },
            Existing: { type: 'object', properties: { name: { type: 'string' } } },
            'Outer+InnerRequest': nestedSchema,
          },
        },
      }),
    );

    expect(normalized.paths?.['/']?.get?.operationId).toBe('getRoot');
    expect(normalized.paths?.['/']?.post?.operationId).toBe('post');
    expect(normalized.paths?.['/api/v1/listings/{listingId}:publish']?.get?.operationId).toBe(
      'listingsGetForGetListingsByListingIdPublish',
    );
    expect(normalized.paths?.['/api/v1/listings/{listingId}:publish']?.post?.operationId).toBe(
      'listingsGetForPostListingsByListingIdPublish',
    );
    expect(normalized.paths?.['/api/v1/listings/{listingId}:publish']?.get?.tags).toEqual(['RealEstateListings']);
    expect(normalized.components?.schemas).toHaveProperty('OuterInnerInput');
    expect(normalized.components?.schemas).toHaveProperty('Existing.properties.name');
    expect(normalized.components?.schemas).toHaveProperty('ExistingDto.properties.id');
    expect(normalized.components?.schemas).toHaveProperty(
      'OuterInnerInput.properties.child.$ref',
      '#/components/schemas/ExistingDto',
    );
    expect(normalized.components?.schemas).toHaveProperty(
      'OuterInnerInput.properties.values',
      [{ $ref: '#/components/schemas/ExistingDto' }],
    );
    expect(normalized.components?.schemas).toHaveProperty('OuterInnerInput.properties.raw', null);
    expect(normalizeSchemaName('PagedResult`1[[Acme_Identity_Users_UserDto, Acme.Identity.Users')).toBe(
      'PagedResultOfIdentityUsersUserDto',
    );
    expect(toSchemaTypeName('App.UserResponse')).toBe('UserOutput');
    expect(resolveKnownSchemaTypeName('App.UserDto', new Set(['User']))).toBe('User');
    expect(resolveKnownSchemaTypeName('UserDto', new Set(['UserDto']))).toBe('UserDto');
    expect(resolveKnownSchemaTypeName('MissingDto', new Set(['Other']))).toBeUndefined();
    expect(normalizeSpec(specWithoutPaths()).paths).toBeUndefined();
  });

  it('generates endpoint definitions for auth, form data, refs, defaults, and descriptions', () => {
    const output = generateEndpoints(
      baseSpec({
        security: [{ bearer: [] }],
        paths: {
          '/missing': undefined,
          '/files/{fileId}': {
            parameters: [
              { $ref: '#/components/parameters/Ignored' },
              { name: 'fileId', in: 'path', required: true, schema: { type: 'string' }, description: 'File id' },
            ],
            post: {
              operationId: 'uploadFile',
              tags: ['Files'],
              summary: 'Upload file',
              description: 'Upload file with metadata',
              parameters: [
                { name: 'trace', in: 'header', required: false, schema: { type: 'string' } },
                { name: 'preview', in: 'query', required: true, schema: { type: 'boolean' } },
              ],
              requestBody: {
                required: true,
                content: {
                  'multipart/form-data': { schema: { type: 'object' } },
                },
              },
              responses: {
                '201': {
                  description: 'Created',
                  content: { 'application/json': { schema: { $ref: '#/components/schemas/FileDto' } } },
                },
                '400': { $ref: '#/components/responses/BadRequest' },
              },
            },
          },
          '/files/{fileId}/replace': {
            put: {
              operationId: 'replaceFile',
              description: 'Replace file',
              requestBody: { $ref: '#/components/requestBodies/FileUpload' },
              responses: { '204': { description: 'No content' } },
              security: [],
            },
          },
          '/files/search': {
            get: {
              responses: {
                '302': { description: 'Redirect' },
              },
            },
          },
          '/files/form': {
            post: {
              operationId: 'submitForm',
              requestBody: {
                content: {
                  'application/x-www-form-urlencoded': { schema: { type: 'object' } },
                },
              },
              responses: {},
            },
          },
          '/files/raw': {
            post: {
              operationId: 'submitRaw',
              summary: 'Raw submit',
              description: 'Raw submit',
              requestBody: {
                content: {
                  'text/plain': { schema: { type: 'string' } },
                },
              },
              responses: {},
            },
          },
          '/files/no-responses': {
            get: operationWithoutResponses({
              operationId: 'getNoResponses',
            }),
          },
        },
      }),
    );

    expect(output).toContain('Upload file with metadata');
    expect(output).toContain('export interface UploadFileInput');
    expect(output).toContain('fileId: string');
    expect(output).toContain('preview: boolean');
    expect(output).toContain('body: FormData');
    expect(output).toContain('export type UploadFileOutput = Types.FileDto');
    expect(output).toContain('requiresAuth: true');
    expect(output).toContain('export type ReplaceFileInput');
    expect(output).toContain('requiresAuth: false');
    expect(output).toContain('export type GetFilesSearchOutput = void');
    expect(output).toContain('body?: FormData');
    expect(output).toContain('export type SubmitRawInput = void');
  });

  it('generates type and Zod branches for enums, dictionaries, objects, arrays, and intersections', () => {
    const spec = baseSpec({
      components: {
        schemas: {
          MixedEnum: { type: 'string', enum: ['legacy', 1, true, null], description: 'Mixed literal values' },
          DescribedUnion: {
            description: 'A described union',
            anyOf: [{ type: 'string' }, { type: 'integer' }],
          },
          EmptyIntersection: { allOf: [], description: 'No parts' },
          SingleIntersection: { allOf: [{ type: 'object', properties: { id: { type: 'string' } } }] },
          AnyDictionary: { type: 'object', additionalProperties: true },
          EmptyDictionary: { type: 'object', additionalProperties: {} },
          NumberDictionary: { type: 'object', additionalProperties: { type: 'number' } },
          EmptyObject: { type: 'object' },
          ArrayAlias: { type: 'array', items: { type: 'string' }, description: 'List of strings' },
          ProblemField: {
            type: 'object',
            required: ['required-value'],
            properties: {
              'required-value': { type: 'boolean' },
              optionalValue: { type: 'integer' },
            },
            additionalProperties: { type: 'string' },
          },
        },
      },
    });
    // Preserve the missing-info fallback scenario without changing valid spec types.
    Reflect.set(spec, 'info', undefined);
    const output = generateTypes(spec);

    expect(output).toContain('Generated from: Unknown API');
    expect(output).toContain('API Version: unknown');
    expect(output).toContain('/** Mixed literal values */');
    expect(output).toContain("export type MixedEnum = 'legacy' | 1 | true | null");
    expect(output).toContain("MixedEnumSchema = z.union([z.literal('legacy'), z.literal(1), z.literal(true), z.literal(null)]);");
    expect(output).toContain('/** A described union */');
    expect(output).toContain('/** Zod schema for DescribedUnion. A described union */');
    expect(output).toContain('EmptyIntersectionSchema = z.object({});');
    expect(output).toContain('SingleIntersectionSchema = z.object({');
    expect(output).toContain('[key: string]: any;');
    expect(output).toContain('[key: string]: number | undefined;');
    expect(output).toContain('EmptyObjectSchema = z.object({});');
    expect(output).toContain('export type ArrayAlias = string[];');
    expect(output).toContain("'required-value': boolean;");
    expect(output).toContain("'required-value': z.boolean()");
    expect(output).toContain('optionalValue: z.number().int().optional()');
    expect(output).toContain('.catchall(z.string())');
  });

  it('generates module clients for default modules, validation, auth, and simple passthrough methods', () => {
    const modules = generateModules(
      baseSpec({
        security: [{ bearer: [] }],
        paths: {
          '/missing': undefined,
          '/health': {
            get: {
              operationId: 'getHealth',
              summary: 'Read health',
              responses: { '204': { description: 'No content' } },
            },
          },
          '/listings/{listingId}/publish': {
            post: {
              operationId: 'publishListing',
              tags: ['RealEstate/Listings'],
              description: 'Publish listing',
              parameters: [
                { $ref: '#/components/parameters/Ignored' },
                { name: 'listingId', in: 'path', required: true, schema: { type: 'string' } },
                { name: 'notify', in: 'query', required: false, schema: { type: 'boolean' } },
                { name: 'channel', in: 'query', required: true, schema: { type: 'string' } },
              ],
              requestBody: {
                content: {
                  'application/json': {
                    schema: { $ref: '#/components/schemas/PublishListingInput' },
                  },
                },
              },
              responses: {
                '200': {
                  description: 'OK',
                  content: { 'application/json': { schema: { $ref: '#/components/schemas/ListingOutput' } } },
                },
              },
              security: [],
            },
          },
          '/listings/search': {
            post: {
              tags: ['RealEstate/Listings'],
              requestBody: {
                content: {
                  'application/json': {
                    schema: { type: 'object', properties: { query: { type: 'string' } } },
                  },
                },
              },
              responses: {
                '200': {
                  description: 'OK',
                  content: {
                    'application/json': {
                      schema: { type: 'array', items: { $ref: '#/components/schemas/ListingOutput' } },
                    },
                  },
                },
              },
            },
          },
          '/listings/archive': {
            delete: {
              tags: ['RealEstate/Listings'],
              requestBody: {
                content: {
                  'text/plain': { schema: { type: 'string' } },
                },
              },
              responses: {
                '200': { $ref: '#/components/responses/Ok' },
              },
            },
          },
          '/listings/no-success': {
            get: {
              tags: ['RealEstate/Listings'],
              responses: {
                '404': { description: 'Missing' },
              },
            },
          },
          '/listings/no-responses': {
            get: operationWithoutResponses({
              tags: ['RealEstate/Listings'],
            }),
          },
        },
      }),
    );

    expect(modules.default).toContain('const url = \'/health\';');
    expect(modules.default).toContain('return result as Result<void, ApiError>;');
    expect(modules.default).toContain('requiresAuth: true');
    expect(modules['real-estate-listings']).toContain(
      'async publishListing(listingId: string, body: Types.PublishListingInput, query?: { notify?: boolean; channel: string })',
    );
    expect(modules['real-estate-listings']).toContain('const validatedBody = safeParse(Types.PublishListingInputSchema');
    expect(modules['real-estate-listings']).toContain('safeParse(Types.ListingOutputSchema');
    expect(modules['real-estate-listings']).toContain('requiresAuth: false');
    expect(modules['real-estate-listings']).toContain('async postListingsSearch(body: { query?: string })');
    expect(modules['real-estate-listings']).toContain('body: body');
    expect(modules['real-estate-listings']).toContain('async deleteListingsArchive()');
    expect(modules['real-estate-listings']).toContain('async getListingsNoSuccess()');
    expect(generateModules(specWithoutPaths())).toEqual({});
    expect(generateEndpoints(specWithoutPaths())).toContain(
      'export const endpoints = {',
    );
  });
});
