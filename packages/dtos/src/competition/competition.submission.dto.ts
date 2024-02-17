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
  @ApiProperty({ type: 'string', format: 'binary', required: true })
  file: Express.Multer.File;
}
