import {
  applyDecorators,
  Param,
  ParseUUIDPipe,
  type PipeTransform,
  UseGuards,
  UseInterceptors,
} from '@nestjs/common';
import { type Type } from '@nestjs/common/interfaces';
import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
  RefreshTokenRoute,
  RouteContentClass,
} from '../auth.enum';
import { ApiBearerAuth, ApiUnauthorizedResponse } from '@nestjs/swagger';
import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { PublicRoute } from './public.decorator';
import { AuthGuard, AuthType } from '../guards/auth.guard';
import { RequireRoleInterceptor } from '../interceptors/require-role.interceptor';
import { WithRolesEntity } from '../entities/with-roles.entity';
import { RequireRole } from './has-role.decorator';

export const Auth = <T extends WithRolesEntity>(
  options:
    | typeof OwnerRoute<T>
    | typeof ManagerRoute<T>
    | typeof PublicRoute
    | typeof AuthenticatedRoute
    | typeof RefreshTokenRoute,
): MethodDecorator => {
  const decorators: Array<
    ClassDecorator | MethodDecorator | PropertyDecorator
  > = [];

  // if public, return now
  if (options === PublicRoute) {
    decorators.push(PublicRoute(true));
    return applyDecorators(...decorators);
  }

  // inject guards for authenticated routes
  decorators.push(PublicRoute(false));
  if (options === RefreshTokenRoute) {
    decorators.push(UseGuards(AuthGuard(AuthType.RefreshToken)));
  } else {
    decorators.push(UseGuards(AuthGuard(AuthType.AccessToken)));
  }
  decorators.push(ApiBearerAuth());
  decorators.push(UseInterceptors(AuthUserInterceptor));
  decorators.push(ApiUnauthorizedResponse({ description: 'Unauthorized' }));

  // if options is RouteContentClass<T>
  if (options instanceof RouteContentClass) {
    decorators.push(RequireRole(options.role, options.type));
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
