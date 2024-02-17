import {
  applyDecorators,
  Param,
  ParseUUIDPipe,
  type PipeTransform,
  UseGuards,
  UseInterceptors,
} from '@nestjs/common';
import { type Type } from '@nestjs/common/interfaces';
import { ApiBearerAuth, ApiUnauthorizedResponse } from '@nestjs/swagger';

import { AuthGuard } from '../guards/auth.guard';
// import { RolesGuard } from '../guards/roles.guard';
import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { Public } from './public.decorator';
// import { Roles } from './roles.decorator';
// import { RoleType } from "../../dtos/constants/role-type";

export const Auth = (
  // roles: RoleType[] = [],
  options?: Partial<{ public: boolean }>,
): MethodDecorator => {
  const isPublic = options?.public;

  return applyDecorators(
    // Roles(roles),
    // UseGuards(AuthGuard({ public: isPublic }), RolesGuard),
    UseGuards(AuthGuard({ public: isPublic })),
    ApiBearerAuth(),
    UseInterceptors(AuthUserInterceptor),
    ApiUnauthorizedResponse({ description: 'Unauthorized' }),
    Public(isPublic),
  );
}

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
