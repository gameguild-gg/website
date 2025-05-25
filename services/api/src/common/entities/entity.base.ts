import { CreateDateColumn, PrimaryGeneratedColumn, UpdateDateColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEmpty, IsOptional, IsUUID } from 'class-validator';
import { Field, ID, ObjectType } from '@nestjs/graphql';

import { CrudValidationGroups } from '@dataui/crud';

@ObjectType({ isAbstract: true })
export abstract class EntityBase {
  @Field(() => ID)
  @ApiProperty({ type: 'string', format: 'uuid', required: false })
  @PrimaryGeneratedColumn('uuid')
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  @IsUUID('4')
  readonly id: string;

  @Field()
  @ApiProperty({ type: 'string', format: 'date-time' })
  @CreateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly createdAt: Date;

  @Field()
  @ApiProperty({ type: 'string', format: 'date-time' })
  @UpdateDateColumn({ type: 'timestamp' })
  @IsOptional()
  @IsEmpty({
    groups: [CrudValidationGroups.CREATE, CrudValidationGroups.UPDATE],
  })
  readonly updatedAt: Date;
}
