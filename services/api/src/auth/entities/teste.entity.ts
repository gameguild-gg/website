// import {Column, Entity, getMetadataArgsStorage, ManyToOne, PrimaryGeneratedColumn} from "typeorm";
// import {snakeCase} from "typeorm/util/StringUtils";
//
// const permissions = (name: string, target: Function) => {
//   getMetadataArgsStorage().tables.push({
//     target: Function,
//     name: name,
//     type: 'regular',
//   });
// }
//
// const permissionEntities: Function[] = [];
//
// export const WithPermissions = (): ClassDecorator => function (target: Function) {
//   const entityName = snakeCase(target.name);
//   // TODO: Get the name of the entity from TypeORM @Entity() decorator.
//   const permissionsTableName = `${entityName}_permissions`;
//
//   @Entity(permissionsTableName)
//   class PermissionEntity extends PermissionBase {
//     @ManyToOne(() => target, {eager: true})
//     targetEntity: typeof target;
//   }
//
//   permissionEntities.push(PermissionEntity);
//
//   // TODO: modify the prototype of the class to include a getter for the permissions
//   // const prototype = target.prototype;
//   // if (!prototype.permissions) {
//   //   Object.defineProperty(prototype, 'permissions', {
//   //     get() {
//   //       return this.manager.getRepository(PermissionEntity).find({where: {targetEntity: this},});
//   //     },
//   //     enumerable: true,
//   //   });
//   // }
// };
//
// export class PermissionBase {
//   @PrimaryGeneratedColumn('uuid')
//   id: string;
//
//   @Column()
//   permission: string;
//
//   @Column()
//   description: string;
//
//   @Column()
//   allowed: boolean;
// }
//
// export class PermissionEntity extends PermissionBase {
//   @ManyToOne(() => ExampleEntity, {eager: true})
//   targetEntity: ExampleEntity;
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
// export {permissionEntities};
