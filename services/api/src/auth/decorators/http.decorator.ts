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
import { AuthUserInterceptor } from '../interceptors/auth-user-interceptor.service';
import { Public } from './public.decorator';

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
      // public routes
      public: true;
      content?: never;
      system?: never;
    }
  | {
      // just logged routes
      public: false;
      content?: never;
      system?: never;
    }
  | {
      // System roles
      public?: never;
      content?: never;
      system: SystemRoles;
    }
  | {
      // Content roles
      public?: never;
      content: ContentRoles;
      system?: never;
    };
// todo improve this!!!
export const Auth = (
  // roles: RoleType[] = [],
  options: RouteRoles,
): MethodDecorator => {
  const isPublic = options.public;

  return applyDecorators(
    // Roles(roles),
    // UseGuards(AuthGuard({ public: isPublic }), RolesGuard),
    UseGuards(AuthGuard({ public: isPublic })),
    ApiBearerAuth(),
    UseInterceptors(AuthUserInterceptor),
    ApiUnauthorizedResponse({ description: 'Unauthorized' }),
    Public(isPublic),
  );
};

export function UUIDParam(
  property: string,
  ...pipes: Array<Type<PipeTransform> | PipeTransform>
): ParameterDecorator {
  return Param(property, new ParseUUIDPipe({ version: '4' }), ...pipes);
}
