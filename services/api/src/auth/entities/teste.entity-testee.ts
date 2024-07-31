// import {
//   Column,
//   Entity,
//   getMetadataArgsStorage,
//   Index,
//   ManyToOne,
//   PrimaryGeneratedColumn,
// } from 'typeorm';
// import { snakeCase } from 'typeorm/util/StringUtils';
// import { EntityBase } from '../../common/entities/entity.base';
// import { UserEntity } from '../../user/entities';
// import { ApiProperty } from '@nestjs/swagger';
// import { IsSlug } from '../../common/decorators/isslug.decorator';
// import { IsString } from 'class-validator';
//
// const permissionEntities: Function[] = [];
//
//
// @Entity('user_tag')
// class UserTag {
//   @ManyToOne(() => UserEntity)
//   user: UserEntity;
// }
//
// export const WithPermissions = (): ClassDecorator =>
//   function (target: Function) {
//     const entityName = snakeCase(target.name);
//     // TODO: Get the name of the entity from TypeORM @Entity() decorator.
//     const permissionsTableName = `${entityName}_permissions`;
//
//     @Entity(permissionsTableName)
//     class PermissionEntity extends PermissionBase {
//       @ManyToOne(() => target, { eager: true })
//       targetEntity: typeof target;
//     }
//
//     permissionEntities.push(PermissionEntity);
//
//     // TODO: modify the prototype of the class to include a getter for the permissions
//     // const prototype = target.prototype;
//     // if (!prototype.permissions) {
//     //   Object.defineProperty(prototype, 'permissions', {
//     //     get() {
//     //       return this.manager.getRepository(PermissionEntity).find({where: {targetEntity: this},});
//     //     },
//     //     enumerable: true,
//     //   });
//     // }
//   };
//
// export class PermissionDescription extends EntityBase {
//   @Column({ type: 'varchar', length: 63 })
//   @Index({ unique: true })
//   permission: string;
//
//   @Column({ type: 'varchar', length: 255 })
//   description: string;
// }
//
// export class PermissionEntity<T> extends PermissionDescription {
//   // pass T as type to the targetEntity
//   @ManyToOne(() => T, { eager: true })
//   targetEntity: typeof T;
// }
//
// @Entity('example_entity')
// @WithPermissions()
// export class ExampleEntity {
//   @PrimaryGeneratedColumn('uuid')
//   id: string;
//
//   @Column()
//   name: string;
//
//   @Column()
//   description: string;
//
//   // TODO: should move to a base class like secure entity. something like that to ensure that all entities have permissions.
//   // permissions?: Promise<PermissionEntity[]>; // Acesso às permissões através de um Promise
// }
//
// @Entity('pokemon')
// @WithPermissions()
// export class PokemonEntity {
//   @PrimaryGeneratedColumn('uuid')
//   id: string;
//
//   @Column()
//   name: string;
//
//   @Column()
//   description: string;
//
//   permissions?: Promise<PermissionEntity[]>; // Acesso às permissões através de um Promise
// }
//
// @Entity('xman')
// @WithPermissions()
// export class XManEntity {
//   @PrimaryGeneratedColumn('uuid')
//   id: string;
//
//   @Column()
//   name: string;
//
//   @Column()
//   description: string;
//
//   permissions?: Promise<PermissionEntity[]>; // Acesso às permissões através de um Promise
// }
//
// export { permissionEntities };

// import { Column, Entity, Index, ManyToOne, Unique } from 'typeorm';
// import { EntityBase } from '../../common/entities/entity.base';
// import { ApiProperty } from '@nestjs/swagger';
// import { UserRoleResourceDto } from '../dtos/user-role-resource.dto';
// import { UserEntity } from '../../user/entities';
// import { IsEnum, IsNotEmpty, IsString, IsUUID } from 'class-validator';
//
// // // todo: this table is nod sharded, ideally it should be sharded by content type but I spent 1 week on that and it is not working as I imagined
// // // please do not try to change this, it will be better to migrate to the a whole new structure when it is ready
// // @Entity({ name: 'user_roles' })
// // // todo: do note use string. get "user", "role", "resourceId" and "resourceType" from the UserRoleResourceDto
// // @Unique('user_role_resource', ['user', 'role', 'resourceId', 'resourceType'])
// // export class UserRoleResourceEntity
// //   extends EntityBase
// //   implements UserRoleResourceDto
// // {
// //   @ApiProperty({ type:  () => UserEntity })
// //   @ManyToOne(() => UserEntity, { lazy: true })
// //   @IsNotEmpty({ message: '{user: {id: string}} is required' })
// //   user: UserEntity;
// //
// //   @ApiProperty({ enum: ContentUserRolesEnum })
// //   @IsNotEmpty({ message: 'role is required' })
// //   @IsEnum(ContentUserRolesEnum, {
// //     message: 'role must be a valid ContentUserRolesEnum',
// //   })
// //   @Column({
// //     enum: ContentUserRolesEnum,
// //     default: ContentUserRolesEnum.OWNER,
// //     nullable: false,
// //   })
// //   @Index({ unique: false })
// //   role: ContentUserRolesEnum;
// //
// //   @ApiProperty()
// //   @IsNotEmpty({ message: 'resourceId is required' })
// //   @IsUUID('4', { message: 'resourceId must be a valid UUID v4' })
// //   @Column({ nullable: false, type: 'uuid' })
// //   @Index({ unique: false })
// //   resourceId: string;
// //
// //   @ApiProperty()
// //   @IsNotEmpty({ message: 'resourceType is required' })
// //   @IsString({ message: 'resourceType must be a string' })
// //   @Column({ nullable: false, type: 'varchar', length: 255 })
// //   @Index({ unique: false })
// //   resourceType: string;
// // }
//
// // export function createUserRoleContentEntity<T extends WithRolesEntity>(
// //   contentType: new () => T,
// // ) {
// //   @Entity({ name: `${contentType.name as string}_permissions` })
// //   class UserRoleContentEntity extends EntityBase implements UserRoleDto {
// //     @ApiProperty({ type:  () => UserEntity })
// //     @ManyToOne(() => UserEntity, { lazy: true })
// //     user: UserEntity;
// //
// //     @ApiProperty({ enum: ContentUserRolesEnum })
// //     @Column({
// //       enum: ContentUserRolesEnum,
// //       default: ContentUserRolesEnum.EDITOR,
// //     })
// //     @Index({ unique: false })
// //     role: ContentUserRolesEnum;
// //
// //     // todo: implement the reverse relation
// //     @ApiProperty({ type:  () => contentType })
// //     @ManyToOne(() => contentType, { lazy: true })
// //     @Type(() => contentType)
// //     content: T;
// //   }
// //
// //   return UserRoleContentEntity;
// // }
//
// // export function EntityWithPermissions(): ClassDecorator {
// //   // todo: improve this typing to be newable function that returns a derived class from EntityBase
// //   return function (target: NewableFunction) {
// //     permissionEntities.push(createUserRoleContentEntity(target));
// //   };
// // }
//
// // @EntityWithPermissions('teste')
// // export class TesteEntity extends EntityBase implements WithRolesEntity {
// //   @ApiProperty({ type:  () => UserRoleContentEntity })
// //   @ManyToOne(() => UserRoleContentEntity, { lazy: true })
// //   roles: UserRoleContentEntity[];
// // }
//
// // export { permissionEntities };
