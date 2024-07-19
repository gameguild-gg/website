import { SetMetadata } from '@nestjs/common';
import { EditorsEntity } from '../entities/editor.entity';

export const IS_EDITOR_KEY = 'isOwner';
export const ENTITY_CLASS_KEY = 'entityClass';

export type EntityClassWithEditorsField<T extends EditorsEntity> = new (
  ...args: any[]
) => T;

export const IsEditor = <T extends EditorsEntity>(
  entityClass: EntityClassWithEditorsField<T>,
) => {
  return (
    target: NonNullable<unknown>,
    key?: string | symbol,
    descriptor?: TypedPropertyDescriptor<any>,
  ) => {
    SetMetadata(IS_EDITOR_KEY, true)(target, key, descriptor);
    SetMetadata(ENTITY_CLASS_KEY, entityClass)(target, key, descriptor);
  };
};
