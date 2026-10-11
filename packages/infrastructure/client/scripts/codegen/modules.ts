/**
 * Module Code Generator
 *
 * Generates module-grouped endpoint files based on OpenAPI tags.
 */

import type { OpenApiSpec } from '../fetch-spec.js';
import type { OpenAPIV3 } from 'openapi-types';
import { toPascalCase, toCamelCase } from '../utils/naming.js';
import { qualifyType } from '../utils/type-qualify.js';
import { formatJsDocLines } from '../utils/jsdoc.js';
import { TypeMapperChain } from './strategies/SchemaTypeMapper.js';
import { ALLOW_ANONYMOUS_EXTENSION, HTTP_METHODS } from './constants.js';
import { binaryResponseMediaType } from './binary-response.js';

interface ModuleEndpoint {
  operationId: string;
  method: string;
  path: string;
  summary?: string;
  description?: string;
  parameters: Array<{
    name: string;
    in: string;
    required: boolean;
    type: string;
  }>;
  requestBodyType?: string;
  requestBodySchema?: string;
  responseType: string;
  responseSchema?: string;
  binaryMediaType?: string;
  requiresAuth: boolean;
}

/**
 * Generate module files grouped by OpenAPI tags
 */
export function generateModules(spec: OpenApiSpec): Record<string, string> {
  const typeMapper = new TypeMapperChain();
  const moduleMap = new Map<string, ModuleEndpoint[]>();

  // Group endpoints by tag
  for (const [path, pathItem] of Object.entries(spec.paths || {})) {
    if (!pathItem) continue;

    for (const method of HTTP_METHODS) {
      const operation = pathItem[method] as OpenAPIV3.OperationObject | undefined;
      if (!operation) continue;

      const tags = operation.tags || ['Default'];
      const primaryTag = tags[0];
      const moduleName = toModuleName(primaryTag);

      if (!moduleMap.has(moduleName)) {
        moduleMap.set(moduleName, []);
      }

      const endpoint = extractModuleEndpoint(operation, method, path, pathItem, spec, typeMapper);
      moduleMap.get(moduleName)!.push(endpoint);
    }
  }

  // Generate code for each module
  const modules: Record<string, string> = {};

  for (const [moduleName, endpoints] of moduleMap) {
    modules[moduleName] = generateModuleCode(moduleName, endpoints);
  }

  return modules;
}

/**
 * Extract endpoint info for module generation
 */
function extractModuleEndpoint(
  operation: OpenAPIV3.OperationObject,
  method: string,
  path: string,
  pathItem: OpenAPIV3.PathItemObject,
  spec: OpenApiSpec,
  typeMapper: TypeMapperChain,
): ModuleEndpoint {
  const parameters: ModuleEndpoint['parameters'] = [];

  // Extract parameters
  const allParams = [...(pathItem.parameters || []), ...(operation.parameters || [])];
  for (const param of allParams) {
    if ('$ref' in param) continue;

    const paramObj = param as OpenAPIV3.ParameterObject;
    parameters.push({
      name: paramObj.name,
      in: paramObj.in,
      required: paramObj.required || paramObj.in === 'path',
      type: typeMapper.map(paramObj.schema as OpenAPIV3.SchemaObject),
    });
  }

  // Extract request body type
  let requestBodyType: string | undefined;
  let requestBodySchema: string | undefined;
  if (operation.requestBody && !('$ref' in operation.requestBody)) {
    const body = operation.requestBody as OpenAPIV3.RequestBodyObject;
    const jsonContent = body.content['application/json'];
    if (jsonContent?.schema) {
      requestBodyType = typeMapper.map(jsonContent.schema as OpenAPIV3.SchemaObject);
      // Extract schema name for validation
      if ('$ref' in jsonContent.schema) {
        requestBodySchema = jsonContent.schema.$ref.replace('#/components/schemas/', '') + 'Schema';
      }
    }
  }

  // Extract response type
  let responseType = 'void';
  let responseSchema: string | undefined;
  let binaryMediaType: string | undefined;
  const successResponse = Object.entries(operation.responses || {}).find(([code]) => code.startsWith('2'));
  if (successResponse) {
    const [, response] = successResponse;
    if (!('$ref' in response)) {
      const responseObj = response as OpenAPIV3.ResponseObject;
      const jsonContent = responseObj.content?.['application/json'];
      if (jsonContent?.schema) {
        responseType = typeMapper.map(jsonContent.schema as OpenAPIV3.SchemaObject);
        // Extract schema name for validation
        if ('$ref' in jsonContent.schema) {
          responseSchema = jsonContent.schema.$ref.replace('#/components/schemas/', '') + 'Schema';
        }
      } else {
        binaryMediaType = binaryResponseMediaType(responseObj.content);
        if (binaryMediaType) responseType = 'Blob';
      }
    }
  }

  // Check if auth is required
  const security = operation.security || spec.security || [];
  const allowAnonymous = (operation as { [key: string]: unknown })[ALLOW_ANONYMOUS_EXTENSION] === true;
  const requiresAuth = security.length > 0 && !allowAnonymous;

  return {
    operationId: operation.operationId || `${method}${path.replace(/\//g, '_')}`,
    method: method.toUpperCase(),
    path,
    summary: operation.summary,
    description: operation.description,
    parameters,
    requestBodyType,
    requestBodySchema,
    responseType,
    responseSchema,
    binaryMediaType,
    requiresAuth,
  };
}

/**
 * Generate code for a single module
 */
function generateModuleCode(moduleName: string, endpoints: ModuleEndpoint[]): string {
  const className = toPascalCase(moduleName) + 'Module';
  const lines: string[] = [];

  lines.push(`/**
 * @game-guild/client - ${toPascalCase(moduleName)} Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */
`);

  // Generate module class
  lines.push(`export class ${className} {`);
  lines.push(`  constructor(private readonly client: ApiClient) {}`);
  lines.push('');

  // Generate method for each endpoint
  for (const endpoint of endpoints) {
    lines.push(generateEndpointMethod(endpoint));
    lines.push('');
  }

  lines.push('}');
  lines.push('');

  // Export factory function
  lines.push(`export function create${className}(client: ApiClient): ${className} {`);
  lines.push(`  return new ${className}(client);`);
  lines.push('}');

  return lines.join('\n');
}

/**
 * Generate a single endpoint method
 */
function generateEndpointMethod(endpoint: ModuleEndpoint): string {
  const methodName = toCamelCase(endpoint.operationId);
  const lines: string[] = [];

  // JSDoc
  lines.push('  /**');
  if (endpoint.summary?.trim()) {
    lines.push(...formatJsDocLines(endpoint.summary, '  '));
  }
  if (endpoint.description?.trim() && endpoint.description !== endpoint.summary) {
    lines.push('   *');
    lines.push(...formatJsDocLines(endpoint.description, '  '));
  }
  lines.push('   */');

  // Method signature
  const params = buildMethodParams(endpoint);
  const returnType = `Promise<Result<${qualifyResponseType(endpoint.responseType)}, ApiError>>`;

  lines.push(`  async ${methodName}(${params}): ${returnType} {`);

  // Build URL with path parameters
  const pathParams = endpoint.parameters.filter((p) => p.in === 'path');
  if (pathParams.length > 0) {
    let urlTemplate = endpoint.path;
    for (const param of pathParams) {
      urlTemplate = urlTemplate.replace(`{${param.name}}`, `\${${param.name}}`);
    }
    lines.push(`    const url = \`${urlTemplate}\`;`);
  } else {
    lines.push(`    const url = '${endpoint.path}';`);
  }

  // Build request options
  const queryParams = endpoint.parameters.filter((p) => p.in === 'query');
  const hasQuery = queryParams.length > 0;
  const hasBody = !!endpoint.requestBodyType;

  // Add request body validation if schema exists
  if (hasBody && endpoint.requestBodySchema) {
    lines.push('');
    lines.push(`    // Validate request body`);
    lines.push(`    const validatedBody = safeParse(Types.${endpoint.requestBodySchema}, body, 'request');`);
  }

  lines.push('');
  lines.push(`    const result = await this.client.request({`);
  lines.push(`      method: '${endpoint.method}',`);
  lines.push(`      path: url,`);
  if (endpoint.binaryMediaType) {
    lines.push(`      responseType: 'blob',`);
    lines.push(`      headers: { Accept: ${JSON.stringify(endpoint.binaryMediaType)} },`);
  }
  if (hasQuery) {
    lines.push(`      params: query,`);
  }
  if (hasBody) {
    const bodyVar = endpoint.requestBodySchema ? 'validatedBody' : 'body';
    lines.push(`      body: ${bodyVar},`);
  }
  lines.push(`      requiresAuth: ${endpoint.requiresAuth},`);
  lines.push(`    });`);

  // Add response validation if schema exists
  if (endpoint.responseSchema && endpoint.responseType !== 'void') {
    lines.push('');
    lines.push(`    // Validate response`);
    lines.push(`    if (result.ok) {`);
    lines.push(`      const validatedData = safeParse(Types.${endpoint.responseSchema}, result.data, 'response');`);
    lines.push(`      return { ok: true, data: validatedData };`);
    lines.push(`    }`);
    lines.push('');
    lines.push(`    return result;`);
  } else {
    lines.push('');
    lines.push(`    return result as Result<${qualifyResponseType(endpoint.responseType)}, ApiError>;`);
  }

  lines.push('  }');

  return lines.join('\n');
}

function qualifyResponseType(type: string): string {
  const qualifiedType = qualifyType(type);
  const simpleArray = qualifiedType.match(/^Array<([\w.]+|Record<string, [\w.]+>)>$/);
  return simpleArray ? `${simpleArray[1]}[]` : qualifiedType;
}

/**
 * Build method parameter string
 */
function buildMethodParams(endpoint: ModuleEndpoint): string {
  const requiredParams: string[] = [];
  const optionalParams: string[] = [];

  // Path parameters
  for (const param of endpoint.parameters.filter((p) => p.in === 'path')) {
    requiredParams.push(`${param.name}: ${qualifyType(param.type)}`);
  }

  // Request body
  if (endpoint.requestBodyType) {
    requiredParams.push(`body: ${qualifyType(endpoint.requestBodyType)}`);
  }

  // Query parameters
  const queryParams = endpoint.parameters.filter((p) => p.in === 'query');
  if (queryParams.length > 0) {
    const queryType = queryParams
      .map((p) => {
        const optional = p.required ? '' : '?';
        return `${p.name}${optional}: ${qualifyType(p.type)}`;
      })
      .join('; ');
    optionalParams.push(`query?: { ${queryType} }`);
  }

  return [...requiredParams, ...optionalParams].join(', ');
}

// Utility function

function toModuleName(tag: string): string {
  return tag
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-|-$/g, '');
}
