import {
  CreateDateColumn,
  DeleteDateColumn,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmpty, IsNotEmpty, IsOptional, IsUUID } from 'class-validator';

import { CrudValidationGroups } from '@dataui/crud';

export abstract class EntityBase {
  @ApiProperty({ type: 'string', format: 'uuid' })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
    message: 'error.isEmpty: id must be empty on create and update operations',
  })
  @IsUUID('4', {
    message: 'error.isUUID: id is not a valid UUID',
  })
  readonly id: string;

  @ApiProperty()
  @CreateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
    message:
      'error.isEmpty: createdAt must be empty on create and update operations',
  })
  readonly createdAt: Date;

  @ApiProperty()
  @UpdateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
    message:
      'error.isEmpty: updatedAt must be empty on create and update operations',
  })
  readonly updatedAt: Date;

  @ApiProperty()
  @DeleteDateColumn()
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
    message:
      'error.isEmpty: deletedAt must be empty on create and update operations',
  })
  deletedAt: Date;
}
