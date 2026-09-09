/**
 * OpenAPI Specification Normalizer
 *
 * Normalizes the OpenAPI spec to ensure consistent naming and structure
 * for code generation.
 */

import type { OpenApiSpec } from './fetch-spec.js';
import type { OpenAPIV3 } from 'openapi-types';
import { toCamelCase, toPascalCase, capitalize, sanitizeIdentifier } from './utils/naming.js';
import { HTTP_METHODS, ASP_NET_PATTERNS } from './codegen/constants.js';

/**
 * Normalize the OpenAPI specification
 */
export function normalizeSpec(spec: OpenApiSpec): OpenApiSpec {
  const normalized = structuredClone(spec);

  // Normalize operation IDs
  normalizeOperationIds(normalized);

  // Normalize tag names
  normalizeTagNames(normalized);

  // Normalize schema names
  normalizeSchemaNames(normalized);

  // Clean up Microsoft/ASP.NET specific patterns
  cleanAspNetPatterns(normalized);

  // Canonical ordering — makes output and hash deterministic regardless of
  // which machine/boot/artifact the spec came from.
  canonicalizeOrder(normalized);

  return normalized;
}

/**
 * ponytail: exact key "id" leads; userId/tenantId etc. sort alphabetically
 * like everything else. Add more lead keys if review diffs want them.
 */
const LEADING_PROPERTY_KEYS = ['id'];

function comparePropertyKeys(left: string, right: string): number {
  const leftRank = LEADING_PROPERTY_KEYS.indexOf(left);
  const rightRank = LEADING_PROPERTY_KEYS.indexOf(right);
  if (leftRank !== rightRank) return leftRank === -1 ? 1 : rightRank === -1 ? -1 : leftRank - rightRank;
  return left.localeCompare(right);
}

function sortRecordKeys<T>(record: Record<string, T>, compare: (left: string, right: string) => number): void {
  const sorted = Object.entries(record).sort(([left], [right]) => compare(left, right));
  for (const key of Object.keys(record)) delete record[key];
  for (const [key, value] of sorted) record[key] = value;
}

function canonicalizeOrder(spec: OpenApiSpec): void {
  if (spec.paths) sortRecordKeys(spec.paths as Record<string, unknown>, (l, r) => l.localeCompare(r));

  spec.tags?.sort((left, right) => left.name.localeCompare(right.name));

  canonicalizeSchemaOrder(spec as unknown as Record<string, unknown>);
}

function canonicalizeSchemaOrder(node: unknown): void {
  if (!node || typeof node !== 'object') return;

  if (Array.isArray(node)) {
    for (const item of node) canonicalizeSchemaOrder(item);
    return;
  }

  const record = node as Record<string, unknown>;

  if (record.properties && typeof record.properties === 'object') {
    sortRecordKeys(record.properties as Record<string, unknown>, comparePropertyKeys);
  }

  if (Array.isArray(record.required)) {
    record.required.sort((left: unknown, right: unknown) => String(left).localeCompare(String(right)));
  }

  if (Array.isArray(record.tags)) {
    record.tags.sort((left: unknown, right: unknown) => String(left).localeCompare(String(right)));
  }

  for (const value of Object.values(record)) canonicalizeSchemaOrder(value);
}

/**
 * Ensure all operations have unique, camelCase operation IDs
 */
function normalizeOperationIds(spec: OpenApiSpec): void {
  const entries: Array<{
    key: string;
    path: string;
    method: string;
    operation: OpenAPIV3.OperationObject;
    baseId: string;
    resolvedId: string;
  }> = [];

  for (const [path, pathItem] of Object.entries(spec.paths || {})) {
    if (!pathItem) continue;

    for (const method of HTTP_METHODS) {
      const operation = pathItem[method] as OpenAPIV3.OperationObject | undefined;
      if (!operation) continue;

      const baseId = operation.operationId
        ? normalizeOperationIdName(operation.operationId)
        : generateOperationId(path, method, operation.tags?.[0]);
      const key = `${method.toLowerCase()} ${path}`;
      entries.push({ key, path, method, operation, baseId, resolvedId: baseId });
    }
  }

  entries.sort((left, right) => left.key.localeCompare(right.key));

  const entriesByBaseId = new Map<string, typeof entries>();
  for (const entry of entries) {
    const group = entriesByBaseId.get(entry.baseId) ?? [];
    group.push(entry);
    entriesByBaseId.set(entry.baseId, group);
  }

  for (const group of entriesByBaseId.values()) {
    if (group.length < 2) continue;

    for (const entry of group) {
      entry.resolvedId = `${entry.baseId}For${generateOperationDiscriminator(entry.path, entry.method)}`;
    }
  }

  // A semantic name can still equal another explicit operation ID. Resolve every
  // member of such a collision from its complete method and route, which is stable
  // regardless of the input JSON property order.
  while (true) {
    const entriesByResolvedId = new Map<string, typeof entries>();
    for (const entry of entries) {
      const group = entriesByResolvedId.get(entry.resolvedId) ?? [];
      group.push(entry);
      entriesByResolvedId.set(entry.resolvedId, group);
    }

    const collisions = [...entriesByResolvedId.values()].filter((group) => group.length > 1);
    if (collisions.length === 0) break;

    for (const group of collisions) {
      for (const entry of group) {
        entry.resolvedId = `operationForCode${encodeOperationKey(entry.key)}Route`;
      }
    }
  }

  for (const entry of entries) {
    entry.operation.operationId = entry.resolvedId;
  }
}

function generateOperationDiscriminator(path: string, method: string): string {
  const cleanPath = path
    .replace(/^\/api\/v\d+\/?/i, '')
    .replace(/^\/v\d+\/?/i, '')
    .replace(/\{([^}]+)\}/g, '_by_$1_')
    .replace(/:/g, '_')
    .replace(/\//g, '_')
    .replace(/^_|_$/g, '');
  const pathParts = sanitizeIdentifier(cleanPath).split('_').filter(Boolean);
  const resource = pathParts.length === 0 ? 'Root' : pathParts.map(toPascalCase).join('');

  return `${toPascalCase(method.toLowerCase())}${resource}`;
}

function encodeOperationKey(key: string): string {
  return Array.from(key, (character) => character.codePointAt(0)!.toString(16)).join('X');
}

/**
 * Generate operation ID from path and method
 */
function generateOperationId(path: string, method: string, tag?: string): string {
  // Remove path parameters and clean up
  const cleanPath = path
    .replace(/\{[^}]+\}/g, '') // Remove {param}
    .replace(/^\/api\/v\d+\/?/, '') // Remove /api/v1/
    .replace(/^\/v\d+\/?/, '') // Remove /v1/
    .replace(/:/g, '_') // Convert custom action separators (:revoke) to _
    .replace(/\//g, '_') // Replace / with _
    .replace(/^_|_$/g, ''); // Remove leading/trailing _

  // Sanitize to valid identifier (handles dashes, remaining special chars)
  const sanitized = sanitizeIdentifier(cleanPath);

  const parts = sanitized.split('_').filter(Boolean);

  // Build operation name
  let name: string;
  if (parts.length === 0) {
    name = tag ? `${method}${tag}` : method;
  } else {
    const resource = parts.map(capitalize).join('');
    name = `${method}${resource}`;
  }

  return toCamelCase(name);
}

/**
 * Normalize operation ID name (remove prefixes, ensure camelCase)
 */
function normalizeOperationIdName(operationId: string): string {
  // Remove common prefixes added by code generators
  let normalized = operationId
    .replace(/^api_/i, '')
    .replace(/^v\d+_/i, '')
    .replace(/_controller_/i, '_')
    .replace(/Controller$/i, '');

  // Sanitize to valid identifier (removes colons, special chars)
  normalized = sanitizeIdentifier(normalized);

  return toCamelCase(normalized);
}

/**
 * Normalize tag names to PascalCase
 */
function normalizeTagNames(spec: OpenApiSpec): void {
  const tagMapping = new Map<string, string>();

  // Normalize top-level tags
  if (spec.tags) {
    for (const tag of spec.tags) {
      const normalized = toPascalCase(tag.name);
      tagMapping.set(tag.name, normalized);
      tag.name = normalized;
    }
  }

  // Update tags in operations
  for (const pathItem of Object.values(spec.paths || {})) {
    if (!pathItem) continue;

    for (const method of HTTP_METHODS) {
      const operation = pathItem[method] as OpenAPIV3.OperationObject | undefined;
      if (!operation?.tags) continue;

      operation.tags = operation.tags.map((tag) => tagMapping.get(tag) || toPascalCase(tag));
    }
  }
}

/**
 * Normalize schema names without conflating DTOs with domain entities.
 */
function normalizeSchemaNames(spec: OpenApiSpec): void {
  const schemas = (spec.components as OpenAPIV3.ComponentsObject)?.schemas;
  if (!schemas) return;

  const schemaMapping = new Map<string, string>();
  const originalEntries = Object.entries(schemas);
  const originalNames = new Set(originalEntries.map(([name]) => name));
  const usedNames = new Set<string>();
  const renamedEntries: Array<[string, OpenAPIV3.ReferenceObject | OpenAPIV3.SchemaObject]> = [];

  for (const [oldName, schema] of originalEntries.sort(([left], [right]) => left.localeCompare(right))) {
    const candidate = normalizeSchemaName(oldName);
    let newName = candidate;

    if (usedNames.has(newName) || (originalNames.has(newName) && newName !== oldName)) {
      const rawName = toPascalCase(sanitizeIdentifier(oldName));
      newName = usedNames.has(rawName) ? `${candidate}From${rawName}` : rawName;
    }

    usedNames.add(newName);
    renamedEntries.push([newName, schema]);
    if (newName !== oldName) schemaMapping.set(oldName, newName);
  }

  for (const name of Object.keys(schemas)) delete schemas[name];
  for (const [name, schema] of renamedEntries.sort(([left], [right]) => left.localeCompare(right))) {
    schemas[name] = schema;
  }

  // Update all $ref references
  updateSchemaRefs(spec, schemaMapping);
}

/**
 * Normalize a single schema name
 */
export function normalizeSchemaName(name: string): string {
  const generic = name.match(/^(.+?)`\d+\[\[([^,\]]+)/);
  if (generic) {
    const baseName = normalizeSimpleSchemaName(generic[1]);
    const argumentParts = generic[2].split(/[._]+/).filter(Boolean);
    const productAgnosticParts = argumentParts.length > 1 ? argumentParts.slice(1) : argumentParts;
    const argumentName = normalizeSimpleSchemaName(productAgnosticParts.join('_'));
    return `${baseName}Of${argumentName}`;
  }

  return normalizeSimpleSchemaName(name);
}

function normalizeSimpleSchemaName(name: string): string {
  let normalized = name;

  // Remove any dotted namespace prefix (e.g., GameGuild.Identity.Users.UserDto -> UserDto)
  const lastDotIndex = normalized.lastIndexOf('.');
  if (lastDotIndex !== -1) {
    normalized = normalized.substring(lastDotIndex + 1);
  }

  // Remove nested class separator
  normalized = normalized.replace(/\+/g, '');

  // DTO identity must not depend on which product-specific entities are present.
  // Removing Dto causes common contracts to change names when a product adds User.
  normalized = normalized.replace(/Request$/i, 'Input');
  normalized = normalized.replace(/Response$/i, 'Output');

  // Ensure PascalCase
  return toPascalCase(normalized);
}

export function toSchemaTypeName(name: string): string {
  return toPascalCase(sanitizeIdentifier(normalizeSchemaName(name)));
}

export function resolveKnownSchemaTypeName(name: string, knownSchemaNames?: Set<string>): string | undefined {
  const normalizedName = toSchemaTypeName(name);
  const sanitizedRawName = toPascalCase(sanitizeIdentifier(name));

  if (!knownSchemaNames) {
    return sanitizedRawName;
  }

  if (knownSchemaNames.has(sanitizedRawName)) {
    return sanitizedRawName;
  }

  if (knownSchemaNames.has(normalizedName)) {
    return normalizedName;
  }

  // Compatibility when consuming an older, already-normalized specification.
  const legacyName = normalizedName.replace(/Dto$/i, '');
  if (knownSchemaNames.has(legacyName)) return legacyName;

  return undefined;
}

/**
 * Update all $ref references in the spec
 */
function updateSchemaRefs(spec: OpenApiSpec, mapping: Map<string, string>): void {
  const updateRefs = (obj: unknown): void => {
    if (!obj || typeof obj !== 'object') return;

    if (Array.isArray(obj)) {
      for (const item of obj) {
        updateRefs(item);
      }
      return;
    }

    const record = obj as Record<string, unknown>;

    if ('$ref' in record && typeof record.$ref === 'string') {
      const refMatch = record.$ref.match(/^#\/components\/schemas\/(.+)$/);
      if (refMatch) {
        const oldName = refMatch[1];
        const newName = mapping.get(oldName);
        if (newName) {
          record.$ref = `#/components/schemas/${newName}`;
        }
      }
    }

    for (const value of Object.values(record)) {
      updateRefs(value);
    }
  };

  updateRefs(spec);
}

/**
 * Clean up ASP.NET specific patterns
 */
function cleanAspNetPatterns(spec: OpenApiSpec): void {
  const schemas = (spec.components as OpenAPIV3.ComponentsObject)?.schemas;
  if (!schemas) return;

  // Remove ProblemDetails-related schemas if they exist (we'll use our own)
  for (const name of ASP_NET_PATTERNS.PROBLEM_DETAILS_SCHEMAS) {
    delete schemas[name];
  }
}
