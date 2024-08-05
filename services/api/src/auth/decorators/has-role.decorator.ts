import { SetMetadata } from '@nestjs/common';
import { ContentUserRolesEnum } from '../auth.enum';
import { WithPermissionsEntity } from '../entities/with-roles.entity';

export const REQUIRED_ROLE_KEY = 'role';
export const ENTITY_CLASS_KEY = 'entityClass';

export type EntityClassWithRolesField<T extends WithPermissionsEntity> = new (
  ...args: any[]
) => T;

export const RequireRole = (
  role: ContentUserRolesEnum,
  type: EntityClassWithRolesField<any>, // todo: add a type that inherits the WithPermissionsEntity class
): ClassDecorator => {
  return (
    target: NewableFunction,
    key?: string | symbol,
    descriptor?: TypedPropertyDescriptor<any>,
  ) => {
    SetMetadata(REQUIRED_ROLE_KEY, role)(target, key, descriptor);
    SetMetadata(ENTITY_CLASS_KEY, type)(target, key, descriptor);
  };
};
