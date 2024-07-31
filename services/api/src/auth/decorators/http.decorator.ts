import {
  applyDecorators,
  Param,
  ParseUUIDPipe,
  type PipeTransform,
  UseGuards,
  UseInterceptors,
} from '@nestjs/common';
import { type Type } from '@nestjs/common/interfaces';
import { RouteRoles } from '../auth.enum';
import { ApiBearerAuth, ApiUnauthorizedResponse } from '@nestjs/swagger';
import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { Public } from './public.decorator';
import { AuthGuard } from '../guards/auth.guard';

// todo improve this!!!
export const Auth = (options: RouteRoles): MethodDecorator => {
  const isPublic = Boolean(options?.public);
  const systemRole = options?.system;
  const contentRole = options?.content;

  const decorators: Array<
    ClassDecorator | MethodDecorator | PropertyDecorator
  > = [];

  // specify if it should be public or not
  decorators.push(Public(isPublic));

  // if public, return now
  if (isPublic) return applyDecorators(...decorators);

  // if it is not public, it is required to have a user
  decorators.push(UseGuards(AuthGuard({ public: isPublic })));

  // jwt required
  decorators.push(UseGuards(AuthGuard({ public: isPublic })));
  decorators.push(ApiBearerAuth());
  decorators.push(UseInterceptors(AuthUserInterceptor));
  decorators.push(ApiUnauthorizedResponse({ description: 'Unauthorized' }));

  // if it just requires user, return now
  if (systemRole === undefined && contentRole === undefined)
    return applyDecorators(...decorators);

  // todo: check if it is a system role or a content role
  return applyDecorators(...decorators);
};

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
