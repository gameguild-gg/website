/**
 * Endpoint Code Generator
 *
 * Generates TypeScript endpoint definitions from OpenAPI paths.
 */

import type { OpenApiSpec } from '../fetch-spec.js';
import type { OpenAPIV3 } from 'openapi-types';
import { BaseGenerator } from './core/BaseGenerator.js';
import { TypeMapperChain } from './strategies/SchemaTypeMapper.js';
import { HTTP_METHODS, SUCCESS_STATUS_PREFIX, CONTENT_TYPES, PARAMETER_LOCATIONS, ALLOW_ANONYMOUS_EXTENSION } from './constants.js';
import { toPascalCase } from '../utils/naming.js';
import { qualifyType } from '../utils/type-qualify.js';
import { formatJsDocLines } from '../utils/jsdoc.js';
import { binaryResponseMediaType } from './binary-response.js';

interface EndpointInfo {
  operationId: string;
  method: string;
  path: string;
  tags: string[];
  summary?: string;
  description?: string;
  parameters: ParameterInfo[];
  requestBody?: RequestBodyInfo;
  responses: ResponseInfo[];
  security: string[][];
  allowAnonymous: boolean;
}

interface ParameterInfo {
  name: string;
  in: 'path' | 'query' | 'header' | 'cookie';
  required: boolean;
  type: string;
  description?: string;
}

interface RequestBodyInfo {
  required: boolean;
  contentType: string;
  type: string;
}

interface ResponseInfo {
  statusCode: string;
  description?: string;
  type: string;
}

/**
 * Generate endpoint definitions
 */
export function generateEndpoints(spec: OpenApiSpec): string {
  const generator = new EndpointsGenerator(spec);
  return generator.generate();
}

class EndpointsGenerator extends BaseGenerator {
  private typeMapper = new TypeMapperChain();

  protected getFileDescription(): string {
    return 'Endpoint Definitions';
  }

  protected generateImports(): string {
    return `import type * as Types from './types.gen.js';

/* eslint-disable @typescript-eslint/no-explicit-any */`;
  }

  protected generateContent(): string {
    const endpoints = this.extractEndpoints();
    const lines: string[] = [];

    lines.push('// Endpoint Definitions');
    lines.push('');

    for (const endpoint of endpoints) {
      lines.push(this.generateEndpointDefinition(endpoint));
      lines.push('');
    }

    lines.push(this.generateEndpointRegistry(endpoints));

    return lines.join('\n');
  }

  /**
   * Extract endpoint information from OpenAPI spec
   */
  private extractEndpoints(): EndpointInfo[] {
    const endpoints: EndpointInfo[] = [];

    for (const [path, pathItem] of Object.entries(this.spec.paths || {})) {
      if (!pathItem) continue;

      for (const method of HTTP_METHODS) {
        const operation = pathItem[method] as OpenAPIV3.OperationObject | undefined;
        if (!operation) continue;

        const endpoint: EndpointInfo = {
          operationId: operation.operationId || `${method}${path.replace(/\//g, '_')}`,
          method: method.toUpperCase(),
          path,
          tags: operation.tags || ['Default'],
          summary: operation.summary,
          description: operation.description,
          parameters: this.extractParameters(operation, pathItem),
          requestBody: this.extractRequestBody(operation),
          responses: this.extractResponses(operation),
          security: this.extractSecurity(operation),
          allowAnonymous: this.extractAllowAnonymous(operation),
        };

        endpoints.push(endpoint);
      }
    }

    return endpoints;
  }

  /**
   * Extract parameters from operation
   */
  private extractParameters(operation: OpenAPIV3.OperationObject, pathItem: OpenAPIV3.PathItemObject): ParameterInfo[] {
    const params: ParameterInfo[] = [];

    // Combine path-level and operation-level parameters
    const allParams = [...(pathItem.parameters || []), ...(operation.parameters || [])];

    for (const param of allParams) {
      if ('$ref' in param) continue;

      const paramObj = param as OpenAPIV3.ParameterObject;
      params.push({
        name: paramObj.name,
        in: paramObj.in as 'path' | 'query' | 'header' | 'cookie',
        required: paramObj.required || paramObj.in === PARAMETER_LOCATIONS.PATH,
        type: this.typeMapper.map(paramObj.schema as OpenAPIV3.SchemaObject),
        description: paramObj.description,
      });
    }

    return params;
  }

  /**
   * Extract request body info
   */
  private extractRequestBody(operation: OpenAPIV3.OperationObject): RequestBodyInfo | undefined {
    if (!operation.requestBody) return undefined;
    if ('$ref' in operation.requestBody) return undefined;

    const body = operation.requestBody as OpenAPIV3.RequestBodyObject;
    const content = body.content;

    // Prefer JSON
    const jsonContent = content[CONTENT_TYPES.JSON];
    if (jsonContent) {
      return {
        required: body.required || false,
        contentType: CONTENT_TYPES.JSON,
        type: this.typeMapper.map(jsonContent.schema as OpenAPIV3.SchemaObject),
      };
    }

    // Fallback to form data
    const formContent = content[CONTENT_TYPES.FORM_DATA] || content[CONTENT_TYPES.FORM_URLENCODED];
    if (formContent) {
      return {
        required: body.required || false,
        contentType: CONTENT_TYPES.FORM_DATA,
        type: 'FormData',
      };
    }

    return undefined;
  }

  /**
   * Extract response info
   */
  private extractResponses(operation: OpenAPIV3.OperationObject): ResponseInfo[] {
    const responses: ResponseInfo[] = [];

    for (const [statusCode, response] of Object.entries(operation.responses || {})) {
      if ('$ref' in response) continue;

      const responseObj = response as OpenAPIV3.ResponseObject;
      const content = responseObj.content;

      let type = 'void';
      if (content?.[CONTENT_TYPES.JSON]?.schema) {
        type = this.typeMapper.map(content[CONTENT_TYPES.JSON].schema as OpenAPIV3.SchemaObject);
      } else if (binaryResponseMediaType(content)) {
        type = 'Blob';
      }

      responses.push({
        statusCode,
        description: responseObj.description,
        type,
      });
    }

    return responses;
  }

  /**
   * Extract security requirements
   */
  private extractSecurity(operation: OpenAPIV3.OperationObject): string[][] {
    const security = operation.security || this.spec.security || [];
    return security.map((req) => Object.keys(req));
  }

  /**
   * The API stamps [AllowAnonymous] endpoints with this extension so clients
   * can call them without a token even when the document carries a global
   * security requirement.
   */
  private extractAllowAnonymous(operation: OpenAPIV3.OperationObject): boolean {
    return (operation as { [key: string]: unknown })[ALLOW_ANONYMOUS_EXTENSION] === true;
  }

  /**
   * Generate TypeScript definition for an endpoint
   */
  private generateEndpointDefinition(endpoint: EndpointInfo): string {
    const lines: string[] = [];

    // JSDoc
    if (endpoint.summary?.trim() || endpoint.description?.trim()) {
      lines.push('/**');
      if (endpoint.summary?.trim()) lines.push(...formatJsDocLines(endpoint.summary));
      if (endpoint.description?.trim() && endpoint.description !== endpoint.summary) {
        if (endpoint.summary?.trim()) lines.push(' *');
        lines.push(...formatJsDocLines(endpoint.description));
      }
      lines.push(' */');
    }

    // Input type
    lines.push(this.generateInputType(endpoint));

    // Output type (success response)
    const successResponse = endpoint.responses.find((r) => r.statusCode.startsWith(SUCCESS_STATUS_PREFIX));
    const outputType = successResponse?.type || 'void';

    lines.push(`export type ${toPascalCase(endpoint.operationId)}Output = ${qualifyType(outputType)};`);

    // Endpoint definition
    lines.push(`export const ${endpoint.operationId}Endpoint = {`);
    lines.push(`  operationId: '${endpoint.operationId}' as const,`);
    lines.push(`  method: '${endpoint.method}' as const,`);
    lines.push(`  path: '${endpoint.path}' as const,`);
    lines.push(`  tags: ${JSON.stringify(endpoint.tags)} as const,`);
    lines.push(`  requiresAuth: ${endpoint.security.length > 0 && !endpoint.allowAnonymous},`);
    lines.push(`} as const;`);

    return lines.join('\n');
  }

  /**
   * Generate input type for endpoint
   */
  private generateInputType(endpoint: EndpointInfo): string {
    const pathParams = endpoint.parameters.filter((p) => p.in === PARAMETER_LOCATIONS.PATH);
    const queryParams = endpoint.parameters.filter((p) => p.in === PARAMETER_LOCATIONS.QUERY);
    const hasBody = !!endpoint.requestBody;

    if (pathParams.length === 0 && queryParams.length === 0 && !hasBody) {
      return `export type ${toPascalCase(endpoint.operationId)}Input = void;`;
    }

    const lines: string[] = [];
    lines.push(`export interface ${toPascalCase(endpoint.operationId)}Input {`);

    // Path parameters
    for (const param of pathParams) {
      lines.push(`  ${param.name}: ${qualifyType(param.type)};`);
    }

    // Query parameters
    if (queryParams.length > 0) {
      lines.push(`  query?: {`);
      for (const param of queryParams) {
        const optional = param.required ? '' : '?';
        lines.push(`    ${param.name}${optional}: ${qualifyType(param.type)};`);
      }
      lines.push(`  };`);
    }

    // Request body
    if (hasBody) {
      const bodyOptional = endpoint.requestBody!.required ? '' : '?';
      lines.push(`  body${bodyOptional}: ${qualifyType(endpoint.requestBody!.type)};`);
    }

    lines.push('}');

    return lines.join('\n');
  }

  /**
   * Generate endpoint registry
   */
  private generateEndpointRegistry(endpoints: EndpointInfo[]): string {
    const lines: string[] = [];

    lines.push('/** Registry of all endpoints */');
    lines.push('export const endpoints = {');

    for (const endpoint of endpoints) {
      lines.push(`  ${endpoint.operationId}: ${endpoint.operationId}Endpoint,`);
    }

    lines.push('} as const;');
    lines.push('');
    lines.push('export type EndpointId = keyof typeof endpoints;');

    return lines.join('\n');
  }
}
