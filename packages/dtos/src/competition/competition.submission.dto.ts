import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';
// This is a hack to make Multer available in the Express namespace
import 'multer'; // a hack to make Multer available in the Express namespace

export class CompetitionSubmissionDto {
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidUsername: Username must be a string.' })
  @IsNotEmpty({ message: 'error.invalidUsername: Username must not be empty.' })
  @MaxLength(32, {
    message:
      'error.invalidUsername: Username must be shorter than or equal to 32 characters.',
  })
  @IsAlphanumeric('en-US', {
    message:
      'error.invalidUsername: Username must be alphanumeric without any special characters.',
  })
  username: string;

  @ApiProperty({ type: 'string', required: false })
  @IsString()
  password: string;

  @ApiProperty({ type: 'string', format: 'binary', required: true })
  file: Express.Multer.File;
}
