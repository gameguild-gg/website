import { Column, Entity, Index } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsString, MaxLength } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';

@Entity({ name: 'job_tag' })
export class JobTagEntity extends EntityBase {
  // Name
  @Column({ length: 256, nullable: false, type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty()
  @MaxLength(64, { message: 'error.maxLength: name is too long, max 64' })
  @IsNotEmpty({ message: 'error.isNotEmpty: name is required' })
  @IsString({ message: 'error.isString: name must be a string' })
  name: string;
}
