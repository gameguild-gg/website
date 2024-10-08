import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';
import { UserEntity } from '../../user/entities';
import { ManyToOne } from 'typeorm';

export class JobPostCreateDto {
    
  // Title
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  readonly title: string;

  // Slug
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  readonly slug: string;

  // Summary
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  readonly summary: string;

  // Body
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  readonly body: string;

  // Location
  @ApiProperty({ required: true })
  @MaxLength(64, { message: 'error.maxLength: location is too long, max 64' })
  @IsNotEmpty({ message: 'error.isNotEmpty: location is required' })
  @IsString({ message: 'error.isString: location must be a string' })
  location: string;

  // Owner
  @ApiProperty({ required: true, type: () => UserEntity })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @ManyToOne(() => UserEntity, { nullable: false, eager: false })
  readonly owner: UserEntity;

  // Tags
  // TODO
  
}
