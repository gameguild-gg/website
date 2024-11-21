import { applyDecorators, Type, UseInterceptors } from '@nestjs/common';
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
import { FileInterceptor } from '@nestjs/platform-express';
import { MulterOptions } from '@nestjs/platform-express/multer/interfaces/multer-options.interface';

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

export function ApiFile(options: MulterOptions = { dest: '/tmp/uploads' }) {
  options.dest = options.dest || '/tmp/uploads';
  return (target, propertyKey, descriptor: PropertyDescriptor) => {
    const dtoType = getBodyDtoType(target, propertyKey);

    if (dtoType) {
      ApiExtraModels(dtoType)(target, propertyKey, descriptor);
    }

    applyDecorators(
      ApiConsumes('multipart/form-data'),
      UseInterceptors(FileInterceptor('file', options)),
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
