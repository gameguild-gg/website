import {
  CreateDateColumn,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmpty, IsOptional, IsUUID } from 'class-validator';

import { CrudValidationGroups } from '@dataui/crud';

export abstract class EntityBase {
  @ApiProperty({ type: 'string', format: 'uuid' })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  @IsUUID('4')
  readonly id: string;

  @ApiProperty({ type: 'string', format: 'date-time' })
  @CreateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly createdAt: Date;

  @ApiProperty({ type: 'string', format: 'date-time' })
  @UpdateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly updatedAt: Date;
}
