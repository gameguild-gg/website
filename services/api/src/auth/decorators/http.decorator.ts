import { applyDecorators, Param, ParseUUIDPipe, type PipeTransform, UseGuards, UseInterceptors } from '@nestjs/common';
import { type Type } from '@nestjs/common/interfaces';
import { AuthenticatedRoute, EditorRoute, OwnerRoute, PublicRoute, RefreshTokenRoute, RouteContentClass } from '../auth.enum';
import {
  ApiBadRequestResponse,
  ApiBearerAuth,
  ApiConflictResponse,
  ApiForbiddenResponse,
  ApiInternalServerErrorResponse,
  ApiNotFoundResponse,
  ApiUnauthorizedResponse,
  ApiUnprocessableEntityResponse,
} from '@nestjs/swagger';
import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { PublicRoute as PublicRouteDecorator } from './public.decorator';
import { AuthGuard, AuthType } from '../guards/auth.guard';
import { RequireRoleInterceptor } from '../interceptors/require-role.interceptor';
import { WithRolesEntity } from '../entities/with-roles.entity';
import { RequireRole } from './has-role.decorator';
import { ApiErrorResponseDto } from '../../common/filters/global-http-exception.filter';

// todo: how to make T optional and extract it from the parameter?
export const Auth = <T extends WithRolesEntity>(
  options: typeof OwnerRoute<T> | typeof EditorRoute<T> | typeof PublicRoute | typeof AuthenticatedRoute | typeof RefreshTokenRoute,
): MethodDecorator => {
  const decorators: Array<ClassDecorator | MethodDecorator | PropertyDecorator> = [];

  decorators.push(
    ApiInternalServerErrorResponse({
      description: 'Internal server error',
      type: ApiErrorResponseDto,
    }),
  );

  decorators.push(
    ApiUnprocessableEntityResponse({
      description: 'Unprocessable entity',
      type: ApiErrorResponseDto,
    }),
  );

  decorators.push(
    ApiBadRequestResponse({
      description: 'Bad request',
      type: ApiErrorResponseDto,
    }),
  );

  // if public, return now
  if (options === PublicRoute) {
    decorators.push(PublicRouteDecorator(true));
    return applyDecorators(...decorators);
  }

  // inject guards for authenticated routes
  decorators.push(PublicRouteDecorator(false));
  if (options === RefreshTokenRoute) {
    decorators.push(UseGuards(AuthGuard(AuthType.RefreshToken))); // it is adding this guard correctly
  } else {
    decorators.push(UseGuards(AuthGuard(AuthType.AccessToken)));
  }
  decorators.push(ApiBearerAuth());
  decorators.push(UseInterceptors(AuthUserInterceptor));
  decorators.push(
    ApiUnauthorizedResponse({
      description: 'Unauthorized',
      type: ApiErrorResponseDto,
    }),
  );
  decorators.push(
    ApiForbiddenResponse({
      description: 'Forbidden',
      type: ApiErrorResponseDto,
    }),
  );
  decorators.push(
    ApiNotFoundResponse({
      description: 'Not found',
      type: ApiErrorResponseDto,
    }),
  );
  decorators.push(
    ApiConflictResponse({
      description: 'Already exists',
      type: ApiErrorResponseDto,
    }),
  );

  // if options is RouteContentClass<T>
  if (options instanceof RouteContentClass) {
    decorators.push(RequireRole(options.role, options.type));
    decorators.push(UseInterceptors(RequireRoleInterceptor));
  }

  // todo: add decorators for system roles

  return applyDecorators(...decorators);
};

export function UUIDParam(property: string, ...pipes: Array<Type<PipeTransform> | PipeTransform>): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
