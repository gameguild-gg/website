import {
  CreateDateColumn,
  PrimaryGeneratedColumn,
  UpdateDateColumn,
} from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString, IsUUID } from 'class-validator';

export abstract class EntityBase {
  @ApiProperty()
  @PrimaryGeneratedColumn('uuid')
  @IsString({ message: 'error.isString: id is not a string' })
  @IsUUID('4', { message: 'error.isUUID: id is not a valid UUID' })
  @IsNotEmpty({ message: 'error.isNotEmpty: id must not be empty' })
  readonly id: string;

  // todo: fix the ApiProperty and the type of the createdAt and updatedAt. Most of the times it should be interchangeable with string type

  @ApiProperty()
  @CreateDateColumn({ type: 'timestamp' })
  readonly createdAt: Date;

  @ApiProperty()
  @UpdateDateColumn({ type: 'timestamp' })
  readonly updatedAt: Date;

  constructor(partial: Partial<EntityBase>) {
    Object.assign(this, partial);
  }
}
