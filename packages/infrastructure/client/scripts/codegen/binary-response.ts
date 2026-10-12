import type { OpenAPIV3 } from 'openapi-types';

/** Identify binary response media without treating JSON byte strings as raw files. */
export function binaryResponseMediaType(content: OpenAPIV3.ResponseObject['content']): string | undefined {
  return Object.entries(content ?? {}).find(
    ([mediaType, value]) =>
      mediaType === 'application/zip' ||
      mediaType === 'application/octet-stream' ||
      (value.schema && !('$ref' in value.schema) && value.schema.format === 'binary'),
  )?.[0];
}
