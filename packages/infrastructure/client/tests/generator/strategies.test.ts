import { describe, expect, it } from 'vitest';

import {
  ArrayTypeMapper,
  BooleanTypeMapper,
  IntersectionTypeMapper,
  NumberTypeMapper,
  ObjectTypeMapper,
  ReferenceTypeMapper,
  StringTypeMapper,
  TypeMapperChain,
  UnionTypeMapper,
} from '../../scripts/codegen/strategies/SchemaTypeMapper.js';
import {
  ZodArrayMapper,
  ZodBooleanMapper,
  ZodIntersectionMapper,
  ZodNumberMapper,
  ZodObjectMapper,
  ZodReferenceMapper,
  ZodSchemaMapperChain,
  ZodStringMapper,
  ZodUnionMapper,
} from '../../scripts/codegen/strategies/ZodSchemaMapper.js';

describe('schema type mapper strategies', () => {
  const chain = new TypeMapperChain();

  it('maps references with normalized, raw, unknown, and error branches', () => {
    expect(new ReferenceTypeMapper().canHandle({ $ref: '#/components/schemas/User.Dto' })).toBe(true);
    expect(new ReferenceTypeMapper().canHandle({ type: 'string' })).toBe(false);
    expect(new ReferenceTypeMapper().map({ $ref: '#/components/schemas/User.Dto' })).toBe('UserDto');
    expect(new ReferenceTypeMapper(new Set(['User'])).map({ $ref: '#/components/schemas/App.UserDto' })).toBe('User');
    expect(new ReferenceTypeMapper(new Set(['UserDto'])).map({ $ref: '#/components/schemas/UserDto' })).toBe('UserDto');
    expect(new ReferenceTypeMapper(new Set(['Known'])).map({ $ref: '#/components/schemas/Missing' })).toBe('unknown');
    expect(new ReferenceTypeMapper().map({ $ref: '#/components/schemas/User.Dto', nullable: true } as any)).toBe('UserDto | null');
    expect(() => new ReferenceTypeMapper().map({ type: 'string' })).toThrow('Not a reference schema');
  });

  it('maps primitive type strategies and their nullable/error branches', () => {
    const stringMapper = new StringTypeMapper();
    const numberMapper = new NumberTypeMapper();
    const booleanMapper = new BooleanTypeMapper();

    expect(stringMapper.canHandle({ type: 'string' })).toBe(true);
    expect(stringMapper.canHandle({ type: 'number' })).toBe(false);
    expect(stringMapper.map({ type: 'string', enum: ['draft', 'published'], nullable: true })).toBe("'draft' | 'published' | null");
    expect(stringMapper.map({ type: 'string', format: 'binary' })).toBe('Blob');
    expect(stringMapper.map({ type: 'string', format: 'unknown', nullable: true })).toBe('string | null');
    expect(numberMapper.map({ type: 'integer', nullable: true })).toBe('number | null');
    expect(numberMapper.map({ type: 'number' })).toBe('number');
    expect(booleanMapper.map({ type: 'boolean', nullable: true })).toBe('boolean | null');
    expect(() => stringMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => numberMapper.map({ $ref: '#/components/schemas/Count' })).toThrow('Reference schema');
    expect(() => booleanMapper.map({ $ref: '#/components/schemas/Flag' })).toThrow('Reference schema');
  });

  it('maps array, object, union, and fallback type branches', () => {
    const arrayMapper = new ArrayTypeMapper(chain);
    const objectMapper = new ObjectTypeMapper(chain);
    const unionMapper = new UnionTypeMapper(chain);

    expect(arrayMapper.canHandle({ type: 'array' })).toBe(true);
    expect(arrayMapper.map({ type: 'array', items: { type: 'string' } })).toBe('string[]');
    expect(arrayMapper.map({ type: 'array', nullable: true } as any)).toBe('unknown[] | null');
    expect(objectMapper.canHandle({} as any)).toBe(true);
    expect(objectMapper.map({ type: 'object', additionalProperties: true })).toBe('Record<string, unknown>');
    expect(objectMapper.map({ type: 'object', additionalProperties: { type: 'number' }, nullable: true })).toBe('Record<string, number> | null');
    expect(
      objectMapper.map({
        type: 'object',
        required: ['id'],
        properties: {
          id: { type: 'string' },
          count: { type: 'integer' },
        },
      }),
    ).toBe('{ id: string; count?: number }');
    expect(objectMapper.map({ type: 'object', nullable: true })).toBe('Record<string, unknown> | null');
    expect(unionMapper.map({ oneOf: [{ type: 'string' }, { type: 'number' }], nullable: true })).toBe('string | number | null');
    expect(unionMapper.map({ anyOf: [{ type: 'boolean' }] })).toBe('boolean');
    expect(unionMapper.map({ nullable: true } as any)).toBe(' | null');
    expect(chain.map({ type: 'funky', nullable: true } as any)).toBe('unknown | null');
    expect(chain.map({ type: 'funky' } as any)).toBe('unknown');
    expect(() => arrayMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => objectMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => unionMapper.map({ $ref: '#/components/schemas/Variant' })).toThrow('Reference schema');
  });

  it('preserves compound, nullable, and nested array item types in bracket syntax', () => {
    expect(chain.map({ type: 'array', items: { type: 'string', nullable: true } })).toBe('(string | null)[]');
    expect(chain.map({ type: 'array', nullable: true, items: { type: 'string' } })).toBe('string[] | null');
    expect(chain.map({ type: 'array', items: { type: 'array', items: { type: 'number' } } })).toBe('number[][]');
    expect(chain.map({ type: 'array', items: { oneOf: [{ type: 'string' }, { type: 'number' }] } })).toBe('(string | number)[]');
    expect(chain.map({ type: 'array', items: { allOf: [{ $ref: '#/components/schemas/A' }, { $ref: '#/components/schemas/B' }] } })).toBe('(A & B)[]');
    expect(chain.map({ type: 'array', items: { $ref: '#/components/schemas/User' } })).toBe('User[]');
  });

  it('maps allOf property compositions and preserves nullable', () => {
    const intersectionMapper = new IntersectionTypeMapper(chain);

    expect(intersectionMapper.canHandle({ allOf: [{ $ref: '#/components/schemas/ScoreValue' }] })).toBe(true);
    expect(
      intersectionMapper.map({
        allOf: [{ $ref: '#/components/schemas/ScoreValue' }],
        nullable: true,
      }),
    ).toBe('ScoreValue | null');
    expect(intersectionMapper.map({ allOf: [] })).toBe('unknown');
    expect(() => intersectionMapper.map({ $ref: '#/components/schemas/ScoreValue' })).toThrow('Reference schema');
  });
});

describe('Zod schema mapper strategies', () => {
  const chain = new ZodSchemaMapperChain();

  it('maps references with known-schema and unknown-schema branches', () => {
    expect(new ZodReferenceMapper().canHandle({ $ref: '#/components/schemas/User.Dto' })).toBe(true);
    expect(new ZodReferenceMapper().canHandle({ type: 'string' })).toBe(false);
    expect(new ZodReferenceMapper().map({ $ref: '#/components/schemas/User.Dto' })).toBe('z.lazy(() => UserDtoSchema)');
    expect(new ZodReferenceMapper(new Set(['User'])).map({ $ref: '#/components/schemas/App.UserDto' })).toBe('z.lazy(() => UserSchema)');
    expect(new ZodReferenceMapper(new Set(['Known'])).map({ $ref: '#/components/schemas/Missing' })).toBe('z.unknown()');
    expect(new ZodReferenceMapper().map({ $ref: '#/components/schemas/User.Dto', nullable: true } as any)).toBe('z.lazy(() => UserDtoSchema).nullable()');
    expect(
      new ZodReferenceMapper(new Set(['Known'])).map({
        $ref: '#/components/schemas/Missing',
        nullable: true,
      } as any),
    ).toBe('z.unknown().nullable()');
    expect(() => new ZodReferenceMapper().map({ type: 'string' })).toThrow('Not a reference schema');
  });

  it('maps primitive Zod strategies and their nullable/error branches', () => {
    const stringMapper = new ZodStringMapper();
    const numberMapper = new ZodNumberMapper();
    const booleanMapper = new ZodBooleanMapper();

    expect(stringMapper.canHandle({ type: 'string' })).toBe(true);
    expect(stringMapper.canHandle({ type: 'number' })).toBe(false);
    expect(stringMapper.map({ type: 'string', enum: ['a', 'b'] })).toBe("z.enum(['a', 'b'])");
    expect(stringMapper.map({ type: 'string', format: 'email', minLength: 2, maxLength: 5, pattern: '^a' })).toBe(
      'z.string().email().min(2).max(5).regex(/^a/)',
    );
    expect(stringMapper.map({ type: 'string', format: 'url' })).toBe('z.string().url()');
    expect(stringMapper.map({ type: 'string', nullable: true })).toBe('z.string().nullable()');
    expect(numberMapper.map({ type: 'integer', minimum: 1, maximum: 3, nullable: true })).toBe('z.number().int().min(1).max(3).nullable()');
    expect(numberMapper.map({ type: 'number' })).toBe('z.number()');
    expect(booleanMapper.map({ type: 'boolean', nullable: true })).toBe('z.boolean().nullable()');
    expect(() => stringMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => numberMapper.map({ $ref: '#/components/schemas/Count' })).toThrow('Reference schema');
    expect(() => booleanMapper.map({ $ref: '#/components/schemas/Flag' })).toThrow('Reference schema');
  });

  it('maps array, object, union, and fallback Zod branches', () => {
    const arrayMapper = new ZodArrayMapper(chain);
    const objectMapper = new ZodObjectMapper(chain);
    const unionMapper = new ZodUnionMapper(chain);

    expect(arrayMapper.canHandle({ type: 'array' })).toBe(true);
    expect(arrayMapper.map({ type: 'array', items: { type: 'string' }, minItems: 1, maxItems: 2 })).toBe('z.array(z.string()).min(1).max(2)');
    expect(arrayMapper.map({ type: 'array', nullable: true } as any)).toBe('z.array(z.unknown()).nullable()');
    expect(objectMapper.canHandle({} as any)).toBe(true);
    expect(objectMapper.map({ type: 'object', additionalProperties: true })).toBe('z.record(z.string(), z.unknown())');
    expect(objectMapper.map({ type: 'object', additionalProperties: { type: 'number' }, nullable: true })).toBe('z.record(z.string(), z.number()).nullable()');
    expect(
      objectMapper.map({
        type: 'object',
        properties: {
          ok: { type: 'boolean' },
          'bad-name': { type: 'string' },
        },
      }),
    ).toContain("'bad-name': z.string()");
    expect(objectMapper.map({ type: 'object' })).toBe('z.record(z.string(), z.unknown())');
    expect(unionMapper.map({ oneOf: [] })).toBe('z.unknown()');
    expect(unionMapper.map({ nullable: true } as any)).toBe('z.unknown().nullable()');
    expect(unionMapper.map({ oneOf: [{ type: 'string' }] })).toBe('z.string()');
    expect(unionMapper.map({ anyOf: [{ type: 'string' }, { type: 'number' }], nullable: true })).toBe('z.union([z.string(), z.number()]).nullable()');
    expect(chain.map({ type: 'funky', nullable: true } as any)).toBe('z.unknown().nullable()');
    expect(chain.map({ type: 'funky' } as any)).toBe('z.unknown()');
    expect(() => arrayMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => objectMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
    expect(() => unionMapper.map({ $ref: '#/components/schemas/User' })).toThrow('Reference schema');
  });

  it('maps allOf property compositions and preserves nullable in Zod', () => {
    const intersectionMapper = new ZodIntersectionMapper(chain);

    expect(intersectionMapper.canHandle({ allOf: [{ $ref: '#/components/schemas/ScoreValue' }] })).toBe(true);
    expect(
      intersectionMapper.map({
        allOf: [{ $ref: '#/components/schemas/ScoreValue' }],
        nullable: true,
      }),
    ).toBe('z.lazy(() => ScoreValueSchema).nullable()');
    expect(
      intersectionMapper.map({
        allOf: [{ type: 'string' }, { type: 'string', minLength: 1 }],
      }),
    ).toBe('z.intersection(z.string(), z.string().min(1))');
    expect(intersectionMapper.map({ allOf: [] })).toBe('z.unknown()');
    expect(() => intersectionMapper.map({ $ref: '#/components/schemas/ScoreValue' })).toThrow('Reference schema');
  });
});
