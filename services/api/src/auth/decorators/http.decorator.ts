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


import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { Public } from './public.decorator';

// jwt requirement routes
export enum LoggedRouteFlag {
  LOGGED = 'LOGGED', // default
  PUBLIC = 'PUBLIC',
}

// Common DAC roles
export enum ContentRoles {
  OWNER = 'OWNER', // default for routes for DELETE actions
  MANAGER = 'MANAGER', // can add other editors
  EDITOR = 'EDITOR', // default for PUT, PATCH, POST
  // GUEST = 'GUEST', // todo
  // SUBSCRIBER = 'SUBSCRIBER', // todo
  // FOLLOWER = 'FOLLOWER', // todo
}

// todo: add ROLES for professor, guardian, student, etc

// common RBAC roles
export enum SystemRoles {
  USER = 'USER', // default
  MANAGER = 'MANAGER',
  ADMIN = 'ADMIN',
}

// todo: it is incomplete
type RouteRoles =
  | {
      loggedRoute: LoggedRouteFlag.PUBLIC;
      contentRole?: never;
      systemRole?: never;
    }
  | { systemRole: SystemRoles; contentRole?: never; loggedRoute?: never }
  | { contentRole: ContentRoles; systemRole?: never; loggedRoute?: never }
  | {
      loggedRoute: LoggedRouteFlag.LOGGED;
      systemRole?: never;
      contentRole?: never;
    }
  | { loggedRoute?: never; contentRole?: never; systemRole?: never };
// todo improve this!!!
export const Auth = (
  // roles: RoleType[] = [],
  options?: Partial<{ public: boolean }>,
): MethodDecorator => {
  const isPublic = options?.public;

  return applyDecorators(
    // Roles(roles),
    // UseGuards(AuthGuard({ public: isPublic }), RolesGuard),
    // UseGuards(AuthGuard({ public: isPublic })),
    // ApiBearerAuth(),
    // UseInterceptors(AuthUserInterceptor),
    // ApiUnauthorizedResponse({ description: 'Unauthorized' }),
    // Public(isPublic),
  );
}

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
