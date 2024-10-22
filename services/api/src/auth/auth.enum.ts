import { WithRolesEntity } from './entities/with-roles.entity';
import { AuthType } from './guards';

export enum ContentUserRolesEnum {
  OWNER = 'CONTENT_OWNER', // default for routes for DELETE actions
  EDITOR = 'CONTENT_EDITOR', // default for PUT, PATCH, POST, UPDATE actions
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

export class RouteRolesClass {
  guard?: AuthType;
  system?: SystemRoles;
}

export class RouteContentClass<T extends WithRolesEntity> {
  constructor(role: ContentUserRolesEnum, type: T) {
    this.type = type;
    this.role = role;
  }

  public readonly type: T;
  public readonly role: ContentUserRolesEnum;
}

export const OwnerRoute = <T extends WithRolesEntity>(
  type: T,
): RouteContentClass<T> => {
  return new RouteContentClass(ContentUserRolesEnum.OWNER, type);
};

export const EditorRoute = <T extends WithRolesEntity>(
  type: T,
): RouteContentClass<T> => {
  return new RouteContentClass(ContentUserRolesEnum.EDITOR, type);
};

export const PublicRoute: RouteRolesClass = {
  guard: AuthType.Public,
};

export const AuthenticatedRoute: RouteRolesClass = {
  guard: AuthType.AccessToken,
};

export const RefreshTokenRoute: RouteRolesClass = {
  guard: AuthType.RefreshToken,
};

export const SystemAdminRoute: RouteRolesClass = {
  guard: AuthType.AccessToken,
  system: SystemRoles.ADMIN,
};

export const SystemManagerRoute: RouteRolesClass = {
  guard: AuthType.AccessToken,
  system: SystemRoles.MANAGER,
};
