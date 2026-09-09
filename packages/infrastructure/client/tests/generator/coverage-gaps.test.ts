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

function baseSpec(overrides: Partial<OpenApiSpec> = {}): OpenApiSpec {
  return {
    openapi: '3.0.1',
    info: { title: 'Coverage API', version: '1.0.0' },
    paths: {},
    components: { schemas: {} },
    ...overrides,
  } as OpenApiSpec;
}

describe('generator coverage edge cases', () => {
  it('normalizes duplicate operation ids, root paths, schema aliases, and conflicts', () => {
    const normalized = normalizeSpec(
      baseSpec({
        tags: [{ name: 'real-estate/listings' }],
        paths: {
          '/empty': undefined as any,
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
            'Outer+InnerRequest': {
              type: 'object',
              properties: {
                child: { $ref: '#/components/schemas/ExistingDto' },
                values: [{ $ref: '#/components/schemas/ExistingDto' }] as any,
                external: { $ref: '#/components/parameters/ExistingDto' },
                raw: null as any,
              },
            },
          },
        },
      }),
    );

    expect((normalized.paths['/'] as any).get.operationId).toBe('getRoot');
    expect((normalized.paths['/'] as any).post.operationId).toBe('post');
    expect((normalized.paths['/api/v1/listings/{listingId}:publish'] as any).get.operationId).toBe(
      'listingsGetForGetListingsByListingIdPublish',
    );
    expect((normalized.paths['/api/v1/listings/{listingId}:publish'] as any).post.operationId).toBe(
      'listingsGetForPostListingsByListingIdPublish',
    );
    expect((normalized.paths['/api/v1/listings/{listingId}:publish'] as any).get.tags).toEqual(['RealEstateListings']);
    expect((normalized.components as any).schemas).toHaveProperty('OuterInnerInput');
    expect((normalized.components as any).schemas.Existing.properties).toHaveProperty('name');
    expect((normalized.components as any).schemas.ExistingDto.properties).toHaveProperty('id');
    expect((normalized.components as any).schemas.OuterInnerInput.properties.child.$ref).toBe(
      '#/components/schemas/ExistingDto',
    );
    expect(normalizeSchemaName('PagedResult`1[[Acme_Identity_Users_UserDto, Acme.Identity.Users')).toBe(
      'PagedResultOfIdentityUsersUserDto',
    );
    expect(toSchemaTypeName('App.UserResponse')).toBe('UserOutput');
    expect(resolveKnownSchemaTypeName('App.UserDto', new Set(['User']))).toBe('User');
    expect(resolveKnownSchemaTypeName('UserDto', new Set(['UserDto']))).toBe('UserDto');
    expect(resolveKnownSchemaTypeName('MissingDto', new Set(['Other']))).toBeUndefined();
    expect(normalizeSpec({ openapi: '3.0.1', info: { title: 'No Paths', version: '1.0.0' } } as any).paths).toBeUndefined();
  });

  it('generates endpoint definitions for auth, form data, refs, defaults, and descriptions', () => {
    const output = generateEndpoints(
      baseSpec({
        security: [{ bearer: [] }],
        paths: {
          '/missing': undefined as any,
          '/files/{fileId}': {
            parameters: [
              { $ref: '#/components/parameters/Ignored' } as any,
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
                '400': { $ref: '#/components/responses/BadRequest' } as any,
              },
            },
          },
          '/files/{fileId}/replace': {
            put: {
              operationId: 'replaceFile',
              description: 'Replace file',
              requestBody: { $ref: '#/components/requestBodies/FileUpload' } as any,
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
            get: {
              operationId: 'getNoResponses',
            } as any,
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
    const output = generateTypes(
      baseSpec({
        info: undefined as any,
        components: {
          schemas: {
            MixedEnum: { type: 'string', enum: ['legacy', 1, true, null] as any, description: 'Mixed literal values' },
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
      }),
    );

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
    expect(output).toContain('export type ArrayAlias = Array<string>;');
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
          '/missing': undefined as any,
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
                { $ref: '#/components/parameters/Ignored' } as any,
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
                '200': { $ref: '#/components/responses/Ok' } as any,
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
            get: {
              tags: ['RealEstate/Listings'],
            } as any,
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
    expect(generateModules({ openapi: '3.0.1', info: { title: 'No Paths', version: '1.0.0' } } as any)).toEqual({});
    expect(generateEndpoints({ openapi: '3.0.1', info: { title: 'No Paths', version: '1.0.0' } } as any)).toContain(
      'export const endpoints = {',
    );
  });
});
