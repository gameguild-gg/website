import { applyDecorators, Type } from '@nestjs/common';
import {
  ApiBody,
  ApiConsumes,
  ApiExtraModels,
  getSchemaPath,
} from '@nestjs/swagger';
import {
  ROUTE_ARGS_METADATA,
  PARAMTYPES_METADATA,
} from '@nestjs/common/constants';
import { RouteParamtypes } from '@nestjs/common/enums/route-paramtypes.enum';

function getBodyDtoType(
  instance: Object,
  propertyKey: string | symbol,
): Type<unknown> | null {
  // Get parameter types for the route method
  const paramTypes: Array<Type<unknown>> = Reflect.getMetadata(
    PARAMTYPES_METADATA,
    instance,
    propertyKey,
  );
  const routeArgsMetadata =
    Reflect.getMetadata(
      ROUTE_ARGS_METADATA,
      instance.constructor,
      propertyKey,
    ) || {};

  // Look for a parameter marked as `@Body`
  for (const [key, param] of Object.entries(routeArgsMetadata)) {
    const [paramType] = key.split(':');
    if (Number(paramType) === RouteParamtypes.BODY) {
      return paramTypes[(param as any).index]; // Type assertion here
    }
  }

  return null;
}

export function ApiFile(): MethodDecorator {
  return (target, propertyKey, descriptor: PropertyDescriptor) => {
    const dtoType = getBodyDtoType(target, propertyKey);

    if (dtoType) {
      ApiExtraModels(dtoType)(target, propertyKey, descriptor);
    }

    applyDecorators(
      ApiConsumes('multipart/form-data'),
      ApiBody({
        schema: {
          type: 'object',
          properties: {
            file: {
              type: 'string',
              format: 'binary',
            },
            ...(dtoType
              ? {
                  body: { $ref: getSchemaPath(dtoType) },
                }
              : {}),
          },
        },
      }),
    )(target, propertyKey, descriptor);
  };
}
