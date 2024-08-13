import { Type } from '@nestjs/common/interfaces';
import { WithRolesEntity } from './entities/with-roles.entity';
import { AuthType } from './guards';

export enum ContentUserRolesEnum {
  OWNER = 'CONTENT_OWNER', // default for routes for DELETE actions
  MANAGER = 'CONTENT_MANAGER', // can add other editors
  EDITOR = 'CONTENT_EDITOR', // default for PUT, PATCH, POST
}

export enum ContentUserSubscriberRolesEnum {
  SUBSCRIBER = 'CONTENT_SUBSCRIBER', // paying follower
  FOLLOWER = 'CONTENT_FOLLOWER', // free follower
}

// common RBAC roles
export enum SystemRoles {
  USER = 'SYSTEM_USER', // default
  MANAGER = 'SYSTEM_MANAGER',
  ADMIN = 'SYSTEM_ADMIN',
}

// todo: it is incomplete
export type RouteRoles =
  | {
      // public routes
      guard: AuthType.Public;
      content?: never;
      system?: never;
      entity?: never;
    }
  | {
      // just authenticated routes
      guard: AuthType.RefreshToken | AuthType.AccessToken;
      content?: never;
      system?: never;
      entity?: never;
    }
  | {
      // System roles
      guard?: AuthType.AccessToken;
      content?: never;
      system: SystemRoles;
      entity?: never;
    }
  | {
      // Content roles
      guard?: AuthType.AccessToken;
      content: ContentUserRolesEnum;
      system?: never;
      entity: Type<WithRolesEntity>;
    };
