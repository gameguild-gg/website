import {
  CreateDateColumn,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmpty, IsOptional, IsUUID } from 'class-validator';

import { CrudValidationGroups } from '@dataui/crud';

export abstract class EntityBase {
  @ApiProperty({ type: 'string', format: 'uuid', required: false })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  @IsUUID('4')
  readonly id: string;

  @ApiProperty({ required: false })
  @CreateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly createdAt: Date;

  @ApiProperty({ required: false })
  @UpdateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly updatedAt: Date;
}
