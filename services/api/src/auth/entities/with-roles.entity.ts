import { ApiProperty } from '@nestjs/swagger';
import { Column } from 'typeorm';
import {
  IsArray,
  IsNotEmpty,
  IsString,
  IsUUID,
  ValidateNested,
} from 'class-validator';
import { Type } from 'class-transformer';
import { EntityBase } from '../../common/entities/entity.base';

export class Permissions {
  @ApiProperty()
  @IsString({ message: 'permission must be a string' })
  @IsNotEmpty({ message: 'owner is required' })
  @IsUUID('4', { message: 'owner must be a valid UUID v4' })
  owner: string;

  @ApiProperty()
  @IsArray({ message: 'editors must be an array or strings' })
  @IsString({ each: true, message: 'each editor entry must be a string' })
  @IsUUID('4', {
    each: true,
    message: 'editors must be an array of valid UUID v4',
  })
  editors?: string[];
}

export class WithPermissionsEntity extends EntityBase {
  @ApiProperty({ type: () => Permissions })
  @ValidateNested()
  @Type(() => Permissions)
  @Column({ type: 'jsonb', nullable: true })
  roles: Permissions;
}
