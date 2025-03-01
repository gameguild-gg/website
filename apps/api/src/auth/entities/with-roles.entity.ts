import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { JoinTable, ManyToMany, ManyToOne } from 'typeorm';
import { UserEntity } from '../../user/entities';
import { IsEmpty, IsOptional } from 'class-validator';
import { CrudValidationGroups } from '@dataui/crud';

// to be used in inheritance of entities that require roles
// todo: change this to be more efficient and generic to different types of roles
export class WithRolesEntity extends EntityBase {
  @ApiProperty({ type: () => UserEntity, required: false })
  @ManyToOne(() => UserEntity, { nullable: true, eager: true })
  @JoinTable()
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  owner: UserEntity;

  @ApiProperty({ type: () => UserEntity, isArray: true, required: false })
  @ManyToMany(() => UserEntity, { nullable: true, eager: true })
  @JoinTable()
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  editors?: UserEntity[];
}
