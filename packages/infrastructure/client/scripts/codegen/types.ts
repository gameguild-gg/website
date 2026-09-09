/**
 * Type Code Generator
 *
 * Generates TypeScript interfaces and Zod schemas from OpenAPI schemas.
 */

import type { OpenApiSpec } from '../fetch-spec.js';
import type { OpenAPIV3 } from 'openapi-types';
import { BaseGenerator } from './core/BaseGenerator.js';
import { TypeMapperChain } from './strategies/SchemaTypeMapper.js';
import { ZodSchemaMapperChain } from './strategies/ZodSchemaMapper.js';
import { sanitizeIdentifier, toPascalCase } from '../utils/naming.js';

/**
 * Generate TypeScript types from OpenAPI schemas
 */
export function generateTypes(spec: OpenApiSpec): string {
  const generator = new TypesGenerator(spec);
  return generator.generate();
}

class TypesGenerator extends BaseGenerator {
  private typeMapper = new TypeMapperChain();
  private zodMapper = new ZodSchemaMapperChain();

  protected getFileDescription(): string {
    return 'Types and Zod Schemas';
  }

  protected generateContent(): string {
    const schemas = (this.spec.components as OpenAPIV3.ComponentsObject)?.schemas || {};
    const lines: string[] = [];

    // Add Zod import
    lines.push("import { z } from 'zod';");
    lines.push('');

    // TWO-PASS APPROACH to avoid circular dependency errors:
    // Pass 1: Declare all schema variables (with type annotation to prevent implicit any)
    // Pass 2: Define schema values using z.lazy() for forward/circular refs

    const schemaEntries = Object.entries(schemas).map(([name, schema]) => ({
      originalName: name,
      sanitizedName: toPascalCase(sanitizeIdentifier(name)),
      schema: schema as OpenAPIV3.SchemaObject,
    }));

    // Pass 1: Generate all TypeScript types first
    for (const { sanitizedName, schema } of schemaEntries) {
      const typeCode = this.generateSchemaType(sanitizedName, schema);
      lines.push(typeCode);
      lines.push('');
    }

    // Pass 2a: Declare all Zod schema variables (avoids "used before declaration" errors)
    lines.push('// Zod Schema Declarations (to handle circular references)');
    for (const { sanitizedName } of schemaEntries) {
      lines.push(`export let ${sanitizedName}Schema: z.ZodType<${sanitizedName}>;`);
    }
    lines.push('');

    // Pass 2b: Define all Zod schemas with z.lazy() for references
    lines.push('// Zod Schema Definitions');
    for (const { sanitizedName, schema } of schemaEntries) {
      const zodCode = this.generateZodSchema(sanitizedName, schema);
      lines.push(zodCode);
      lines.push('');
    }

    const knownSchemaNames = new Set(schemaEntries.map(({ sanitizedName }) => sanitizedName));
    const legacyDtoAliases = schemaEntries.flatMap(({ sanitizedName }) => {
      if (!/Dto$/i.test(sanitizedName)) return [];

      const legacyName = sanitizedName.replace(/Dto$/i, '');
      if (!legacyName || knownSchemaNames.has(legacyName)) return [];

      return [{ legacyName, dtoName: sanitizedName }];
    });

    if (legacyDtoAliases.length > 0) {
      lines.push('// Backwards-compatible aliases for names generated before DTO identity was preserved');
      for (const { legacyName, dtoName } of legacyDtoAliases) {
        lines.push(`export type ${legacyName} = ${dtoName};`);
        lines.push(`export { ${dtoName}Schema as ${legacyName}Schema };`);
      }
      lines.push('');
    }

    return lines.join('\n');
  }

  /**
   * Generate TypeScript type for a single schema
   */
  private generateSchemaType(name: string, schema: OpenAPIV3.SchemaObject): string {
    // Handle enums
    if (schema.enum) {
      return this.generateEnumType(name, schema);
    }

    // Handle oneOf/anyOf (union types)
    if (schema.oneOf || schema.anyOf) {
      return this.generateUnionType(name, schema);
    }

    // Handle allOf (intersection types)
    if (schema.allOf) {
      return this.generateIntersectionType(name, schema);
    }

    // Handle objects
    if (schema.type === 'object' || schema.properties) {
      return this.generateInterfaceType(name, schema);
    }

    // Handle primitives and arrays (type aliases)
    return this.generateTypeAlias(name, schema);
  }

  /**
   * Generate enum type
   */
  private generateEnumType(name: string, schema: OpenAPIV3.SchemaObject): string {
    const values = schema.enum!;
    const description = schema.description ? `/** ${schema.description} */\n` : '';

    // Check if all values are strings
    const allStrings = values.every((v) => typeof v === 'string');

    if (allStrings) {
      // Generate string literal union
      const literals = values.map((v) => `'${v}'`).join(' | ');
      return `${description}export type ${name} = ${literals};`;
    }

    // For numeric enums, generate a type union instead of enum
    // This avoids issues with numeric enum member names
    const literals = values.map((v) => (typeof v === 'string' ? `'${v}'` : JSON.stringify(v))).join(' | ');
    return `${description}export type ${name} = ${literals};`;
  }

  /**
   * Generate union type from oneOf/anyOf
   */
  private generateUnionType(name: string, schema: OpenAPIV3.SchemaObject): string {
    const variants = schema.oneOf ?? schema.anyOf!;
    const description = schema.description ? `/** ${schema.description} */\n` : '';

    const types = variants.map((variant) => this.typeMapper.map(variant as OpenAPIV3.SchemaObject)).join(' | ');

    return `${description}export type ${name} = ${types};`;
  }

  /**
   * Generate intersection type from allOf
   */
  private generateIntersectionType(name: string, schema: OpenAPIV3.SchemaObject): string {
    const parts = schema.allOf!;
    const description = schema.description ? `/** ${schema.description} */\n` : '';

    const types = parts.map((part) => this.typeMapper.map(part as OpenAPIV3.SchemaObject)).join(' & ');

    return `${description}export type ${name} = ${types};`;
  }

  /**
   * Generate interface type from object schema
   */
  private generateInterfaceType(name: string, schema: OpenAPIV3.SchemaObject): string {
    const properties = schema.properties || {};
    const required = new Set(schema.required || []);
    const description = schema.description ? `/** ${schema.description} */\n` : '';

    const lines: string[] = [];
    lines.push(`${description}export interface ${name} {`);

    for (const [propName, propSchema] of Object.entries(properties)) {
      const propObj = propSchema as OpenAPIV3.SchemaObject | OpenAPIV3.ReferenceObject;
      const isRequired = required.has(propName);
      const optional = isRequired ? '' : '?';
      const propType = this.typeMapper.map(propObj);

      // Add JSDoc for property description
      const propDescription = 'description' in propObj ? propObj.description : undefined;
      if (propDescription) {
        lines.push(`  /** ${propDescription} */`);
      }

      // Use quotes for property names with special characters
      const safePropName = /^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(propName) ? propName : `'${propName}'`;

      lines.push(`  ${safePropName}${optional}: ${propType};`);
    }

    // Handle additionalProperties
    if (schema.additionalProperties) {
      if (schema.additionalProperties === true) {
        // Use 'any' instead of 'unknown' to be compatible with nullable/optional properties
        lines.push(`  [key: string]: any;`);
      } else {
        const addlProps = schema.additionalProperties as OpenAPIV3.SchemaObject;
        // Empty object schema (additionalProperties: {}) means "any unknown property"
        // Use 'any' to be compatible with nullable/optional primitive properties
        if (Object.keys(addlProps).length === 0) {
          lines.push(`  [key: string]: any;`);
        } else {
          const additionalType = this.typeMapper.map(addlProps);
          lines.push(`  [key: string]: ${additionalType} | undefined;`);
        }
      }
    }

    lines.push('}');

    return lines.join('\n');
  }

  /**
   * Generate type alias for primitives/arrays
   */
  private generateTypeAlias(name: string, schema: OpenAPIV3.SchemaObject): string {
    const description = schema.description ? `/** ${schema.description} */\n` : '';
    const typeString = this.typeMapper.map(schema);

    return `${description}export type ${name} = ${typeString};`;
  }

  /**
   * Generate Zod schema for runtime validation
   */
  private generateZodSchema(name: string, schema: OpenAPIV3.SchemaObject): string {
    const description = schema.description ? `/** Zod schema for ${name}. ${schema.description} */\n` : `/** Zod schema for ${name} */\n`;

    // Handle enums specially for better Zod output
    if (schema.enum) {
      return this.generateZodEnum(name, schema);
    }

    // Handle oneOf/anyOf (union types)
    if (schema.oneOf || schema.anyOf) {
      return this.generateZodUnion(name, schema);
    }

    // Handle allOf (intersection types) - Zod doesn't have direct intersection support
    // Use merge() for object intersections
    if (schema.allOf) {
      return this.generateZodIntersection(name, schema);
    }

    // Handle objects
    if (schema.type === 'object' || schema.properties) {
      return this.generateZodObject(name, schema);
    }

    // Handle primitives and arrays
    const zodSchema = this.zodMapper.map(schema);
    return `${description}${name}Schema = ${zodSchema};`;
  }

  /**
   * Generate Zod enum schema
   */
  private generateZodEnum(name: string, schema: OpenAPIV3.SchemaObject): string {
    const values = schema.enum!;
    const description = schema.description ? `/** Zod schema for ${name}. ${schema.description} */\n` : `/** Zod schema for ${name} */\n`;

    // Check if all values are strings
    const allStrings = values.every((v) => typeof v === 'string');

    if (allStrings && values.length > 0) {
      const literals = values.map((v) => `'${v}'`).join(', ');
      return `${description}${name}Schema = z.enum([${literals}]);`;
    }

    // For non-string enums, use union of literals
    const literals = values
      .map((v) => {
        if (typeof v === 'string') return `z.literal('${v}')`;
        if (typeof v === 'number') return `z.literal(${v})`;
        if (typeof v === 'boolean') return `z.literal(${v})`;
        return `z.literal(${JSON.stringify(v)})`;
      })
      .join(', ');

    return `${description}${name}Schema = z.union([${literals}]);`;
  }

  /**
   * Generate Zod union schema
   */
  private generateZodUnion(name: string, schema: OpenAPIV3.SchemaObject): string {
    const description = schema.description ? `/** Zod schema for ${name}. ${schema.description} */\n` : `/** Zod schema for ${name} */\n`;
    const zodSchema = this.zodMapper.map(schema);
    return `${description}${name}Schema = ${zodSchema};`;
  }

  /**
   * Generate Zod intersection schema using merge()
   */
  private generateZodIntersection(name: string, schema: OpenAPIV3.SchemaObject): string {
    const parts = schema.allOf!;
    const description = schema.description ? `/** Zod schema for ${name}. ${schema.description} */\n` : `/** Zod schema for ${name} */\n`;

    if (parts.length === 0) {
      return `${description}${name}Schema = z.object({});`;
    }

    if (parts.length === 1) {
      const zodSchema = this.zodMapper.map(parts[0] as OpenAPIV3.SchemaObject);
      return `${description}${name}Schema = ${zodSchema};`;
    }

    // Use merge() for object intersections
    const schemas = parts.map((part) => this.zodMapper.map(part as OpenAPIV3.SchemaObject));
    const mergedSchema = schemas.reduce((acc, curr) => `${acc}.merge(${curr})`);

    return `${description}${name}Schema = ${mergedSchema};`;
  }

  /**
   * Generate Zod object schema
   */
  private generateZodObject(name: string, schema: OpenAPIV3.SchemaObject): string {
    const properties = schema.properties || {};
    const required = new Set(schema.required || []);
    const description = schema.description ? `/** Zod schema for ${name}. ${schema.description} */\n` : `/** Zod schema for ${name} */\n`;

    const propEntries: string[] = [];

    for (const [propName, propSchema] of Object.entries(properties)) {
      const propObj = propSchema as OpenAPIV3.SchemaObject | OpenAPIV3.ReferenceObject;
      const isRequired = required.has(propName);
      const zodPropSchema = this.zodMapper.map(propObj);

      // Make optional if not required
      const finalSchema = isRequired ? zodPropSchema : `${zodPropSchema}.optional()`;

      // Use quotes for property names with special characters
      const safePropName = /^[a-zA-Z_$][a-zA-Z0-9_$]*$/.test(propName) ? propName : `'${propName}'`;

      propEntries.push(`  ${safePropName}: ${finalSchema}`);
    }

    let zodSchema: string;

    if (propEntries.length > 0) {
      zodSchema = `z.object({\n${propEntries.join(',\n')}\n})`;
    } else {
      zodSchema = 'z.object({})';
    }

    // Handle additionalProperties
    if (schema.additionalProperties) {
      if (schema.additionalProperties === true) {
        zodSchema += '.catchall(z.unknown())';
      } else {
        const additionalSchema = this.zodMapper.map(schema.additionalProperties as OpenAPIV3.SchemaObject);
        zodSchema += `.catchall(${additionalSchema})`;
      }
    }

    return `${description}${name}Schema = ${zodSchema};`;
  }
}
