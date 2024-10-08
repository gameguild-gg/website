import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';

export class JobPostCreateDto {
    
  // Title
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  @IsAlphanumeric('en-US', {
    message: 'error.invalidTitle: Title must be alphanumeric without any special characters.',
  })
  readonly title: string;

  // Summary
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  @IsAlphanumeric('en-US', {
    message: 'error.invalidTitle: Title must be alphanumeric without any special characters.',
  })
  readonly summary: string;

  // Body
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @IsAlphanumeric('en-US', {
    message: 'error.invalidTitle: Title must be alphanumeric without any special characters.',
  })
  readonly body: string;

  // slug
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidTitle: Title must be a string.' })
  @IsNotEmpty({ message: 'error.invalidTitle: Title must not be empty.' })
  @MaxLength(64, {
    message: 'error.invalidTitle: Title must be shorter than or equal to 64 characters.',
  })
  @IsAlphanumeric('en-US', {
    message: 'error.invalidTitle: Title must be alphanumeric without any special characters.',
  })
  readonly slug: string;

  
}
