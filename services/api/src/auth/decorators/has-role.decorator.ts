import { SetMetadata } from '@nestjs/common';
import { ContentUserRolesEnum } from '../auth.enum';
import { WithRolesEntity } from '../entities/with-roles.entity';

export const REQUIRED_ROLE_KEY = 'role';
export const ENTITY_CLASS_KEY = 'entityClass';

export type EntityClassWithRolesField<T extends WithRolesEntity> = new (...args: any[]) => T;

export const RequireRole = <T extends WithRolesEntity>(
  role: ContentUserRolesEnum,
  type: T, // todo: add a type that inherits the WithPermissionsEntity class
): MethodDecorator => {
  return (target, key, descriptor) => {
    SetMetadata(REQUIRED_ROLE_KEY, role)(target, key, descriptor);
    SetMetadata(ENTITY_CLASS_KEY, type)(target, key, descriptor);
  };
};
