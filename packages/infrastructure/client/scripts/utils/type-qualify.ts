/**
 * Type Qualification Utility
 *
 * Qualifies type references with namespace prefixes for generated code.
 */

const PRIMITIVE_TYPES = new Set([
  'void', 'string', 'number', 'boolean', 'unknown', 'any',
  'null', 'undefined', 'never', 'object', 'Blob', 'FormData',
  'File', 'ReadableStream', 'ArrayBuffer',
]);

const STRING_LITERAL_PATTERN = /^(['"]).*\1$/;
const NUMBER_LITERAL_PATTERN = /^-?\d+(\.\d+)?$/;
const BOOLEAN_LITERAL_TYPES = new Set(['true', 'false']);

/**
 * Qualify a type reference with the Types namespace prefix.
 * Primitives and built-in types are left as-is.
 */
export function qualifyType(type: string, namespace = 'Types'): string {
  // Qualify the element, not the complete bracket-array expression.
  if (type.endsWith('[]')) {
    const item = type.slice(0, -2);
    if (item.startsWith('(') && item.endsWith(')')) {
      return `(${qualifyType(item.slice(1, -1), namespace)})[]`;
    }
    return `${qualifyType(item, namespace)}[]`;
  }

  // Handle inline object types
  if (type.startsWith('{') || type.startsWith('(')) {
    return type;
  }

  // Handle Array<T> generics
  const arrayMatch = type.match(/^Array<(.+)>(.*)$/);
  if (arrayMatch) {
    return `Array<${qualifyType(arrayMatch[1], namespace)}>${arrayMatch[2]}`;
  }

  // Handle union types (e.g. "string | null")
  if (type.includes(' | ')) {
    return type.split(' | ').map(t => qualifyType(t.trim(), namespace)).join(' | ');
  }

  if (type.includes(' & ')) {
    return type.split(' & ').map(t => qualifyType(t.trim(), namespace)).join(' & ');
  }

  // Handle Record<K,V>
  if (type.startsWith('Record<')) {
    return type;
  }

  const baseType = type;

  // Primitives and built-ins don't need namespace
  if (PRIMITIVE_TYPES.has(baseType)) {
    return type;
  }

  // Literal unions generated from OpenAPI enums are already complete types.
  if (
    STRING_LITERAL_PATTERN.test(baseType) ||
    NUMBER_LITERAL_PATTERN.test(baseType) ||
    BOOLEAN_LITERAL_TYPES.has(baseType)
  ) {
    return type;
  }

  // Already qualified
  if (baseType.startsWith('Types.') || baseType.startsWith('Errors.')) {
    return type;
  }

  return `${namespace}.${baseType}`;
}
