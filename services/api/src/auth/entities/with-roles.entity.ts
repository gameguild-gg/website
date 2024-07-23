import { PermissionsDto, WithRolesDto } from '../dtos/with-roles.dto';
import { ApiProperty } from '@nestjs/swagger';
import { Column } from 'typeorm';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';

export class WithRolesEntity extends EntityBase implements WithRolesDto {
  @ApiProperty({ type: () => PermissionsDto })
  @ValidateNested()
  @Type(() => PermissionsDto)
  @Column({ type: 'jsonb', nullable: true })
  roles: PermissionsDto;
}
