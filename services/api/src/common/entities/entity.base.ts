import {
  CreateDateColumn,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmpty, IsNotEmpty, IsOptional, IsUUID } from 'class-validator';

import { CrudValidationGroups } from '@dataui/crud';

export abstract class EntityBase {
  @ApiProperty({ type: 'string', format: 'uuid' })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional({ groups: [CrudValidationGroups.CREATE] })
  @IsNotEmpty({
    message: 'error.isNotEmpty: id must not be empty',
    groups: [CrudValidationGroups.UPDATE],
  })
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE],
    message: 'error.isEmpty: id must be empty on create operations',
  })
  @IsUUID('4', {
    message: 'error.isUUID: id is not a valid UUID',
    groups: [CrudValidationGroups.UPDATE],
  })
  readonly id: string;

  @ApiProperty()
  @CreateDateColumn({ type: 'timestamp' })
  @IsEmpty({
    message: 'error.isEmpty: createdAt must be empty on create operations',
  })
  readonly createdAt: Date;

  @ApiProperty()
  @UpdateDateColumn({ type: 'timestamp' })
  @IsEmpty({
    message: 'error.isEmpty: id must be empty on create operations',
  })
  readonly updatedAt: Date;

  constructor(partial: Partial<EntityBase>) {
    Object.assign(this, partial);
  }
}
