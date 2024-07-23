import { SetMetadata } from '@nestjs/common';
import { WithRolesEntity } from '../entities/with-roles.entity';
import { ContentUserRolesEnum } from '../auth.enum';

export const REQUIRED_ROLE_KEY = 'role';
export const ENTITY_CLASS_KEY = 'entityClass';

export type EntityClassWithRolesField<T extends WithRolesEntity> = new (
  ...args: any[]
) => T;

export const RequireRole = (role: ContentUserRolesEnum): ClassDecorator => {
  return (
    target: NewableFunction,
    key?: string | symbol,
    descriptor?: TypedPropertyDescriptor<any>,
  ) => {
    // todo: Check if the target is a class or a newable function derived from WithRolesEntity
    let entityClass: EntityClassWithRolesField<any> | undefined;

    if (target.prototype instanceof WithRolesEntity) {
      entityClass = target as EntityClassWithRolesField<any>;
    }

    SetMetadata(REQUIRED_ROLE_KEY, role)(target, key, descriptor);
    SetMetadata(ENTITY_CLASS_KEY, entityClass)(target, key, descriptor);
  };
};
