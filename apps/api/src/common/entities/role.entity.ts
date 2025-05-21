import { Column, Entity } from 'typeorm';

import { RoleDto } from '@/common/dtos/role.dto';
import { SerializedRule } from '@/common/dtos/serialized-rule';
import { EntityBase } from '@/common/entities/entity.base';
import { UserEntity } from '@/user/entities/user.entity';

@Entity('role')
export class RoleEntity extends EntityBase implements RoleDto {
  @Column({ type: 'varchar', length: 50, unique: true })
  public readonly name: string;

  // Exemplo de regras em JSON. Você pode definir seu padrão, por exemplo:
  // {
  //   "course": {
  //     "global": {
  //       "read": { "condition": { "isPublished": true } }
  //     },
  //     "owner": {
  //       "update": true,
  //       "delete": false
  //     }
  //   },
  //   "global": {
  //     "emailVerifiedRequired": true
  //   }
  // }

  @Column({ type: 'jsonb', nullable: false })
  public readonly rules: SerializedRule;

  // @ManyToMany(() => UserEntity, (user) => user.roles)
  public readonly users: UserEntity[];
}
