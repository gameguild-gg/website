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
import { PublicRoute } from './public.decorator';
import { AuthGuard, AuthType } from '../guards/auth.guard';
import { RequireRoleInterceptor } from '../interceptors/require-role.interceptor';
import { RequireRole } from './has-role.decorator';
import { InjectUserAsOwnerToRequestInterceptor } from '../../cms/interceptors/inject-user-as-owner-to-request.interceptor';

export const Auth = (options: RouteRoles): MethodDecorator => {
  const decorators: Array<
    ClassDecorator | MethodDecorator | PropertyDecorator
  > = [];

  // if public, return now
  if (options?.guard === AuthType.Public) {
    decorators.push(PublicRoute(true));
    return applyDecorators(...decorators);
  }

  // inject guards for authenticated routes
  decorators.push(PublicRoute(false));
  decorators.push(UseGuards(AuthGuard(options?.guard || AuthType.AccessToken)));
  decorators.push(ApiBearerAuth());
  decorators.push(UseInterceptors(AuthUserInterceptor));
  decorators.push(ApiUnauthorizedResponse({ description: 'Unauthorized' }));

  // inject owner to request body. it assumes the context has user and the content is in the request body
  if (options?.injectOwner)
    decorators.push(UseInterceptors(InjectUserAsOwnerToRequestInterceptor));

  if (options?.content) {
    // add require roles for content
    decorators.push(RequireRole(options.content, options.entity));
    decorators.push(UseInterceptors(RequireRoleInterceptor));
  }

  // todo: add decorators for system roles

  return applyDecorators(...decorators);
};

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
