import { SetMetadata } from '@nestjs/common';
import { OwnerEntity } from '../entities/owner.entity';

export const IS_OWNER_KEY = 'isOwner';
export const ENTITY_CLASS_KEY = 'entityClass';

export type EntityClassWithOwnerField<T extends OwnerEntity> = new (
  ...args: any[]
) => T;

export const IsOwner = <T extends OwnerEntity>(
  entityClass: EntityClassWithOwnerField<T>,
) => {
  return (
    target: NonNullable<unknown>,
    key?: string | symbol,
    descriptor?: TypedPropertyDescriptor<any>,
  ) => {
    SetMetadata(IS_OWNER_KEY, true)(target, key, descriptor);
    SetMetadata(ENTITY_CLASS_KEY, entityClass)(target, key, descriptor);
  };
};
