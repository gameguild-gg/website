import {
  applyDecorators,
  Param,
  ParseUUIDPipe,
  type PipeTransform,
} from '@nestjs/common';
import { type Type } from '@nestjs/common/interfaces';
import { RouteRoles } from '../auth.enum';

// todo improve this!!!
export const Auth = (options: RouteRoles): MethodDecorator => {
  const isPublic = options.public;

  return applyDecorators();
  // Roles(roles),
  // UseGuards(AuthGuard({ public: isPublic }), RolesGuard),
  // UseGuards(AuthGuard({ public: isPublic })),
  // ApiBearerAuth(),
  // UseInterceptors(AuthUserInterceptor),
  // ApiUnauthorizedResponse({ description: 'Unauthorized' }),
  // Public(isPublic),
};

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
