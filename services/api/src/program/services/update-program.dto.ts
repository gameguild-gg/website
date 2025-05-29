import { IsOptional } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { PartialType } from '@nestjs/swagger';
import { CreateProgramDto } from './create-program.dto';

export class UpdateProgramDto extends PartialType(CreateProgramDto) {
  // All fields from CreateProgramDto are automatically made optional by PartialType
}
