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

  return applyDecorators(...decorators);
};

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
