import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { JoinTable, ManyToMany, ManyToOne } from 'typeorm';
import { UserEntity } from '../../user/entities';

// to be used in inheritance of entities that require roles
// todo: change this to be more efficient and generic to different types of roles
export class WithRolesEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity })
  @ManyToOne(() => UserEntity, { nullable: true, eager: false })
  owner: UserEntity;

  @ApiProperty({ type: () => UserEntity, isArray: true })
  @ManyToMany(() => UserEntity, { nullable: true, eager: false })
  @JoinTable()
  editors?: UserEntity[];
}
