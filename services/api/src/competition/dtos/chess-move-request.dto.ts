import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsNotEmpty,
  IsString,
  MaxLength,
} from 'class-validator';

export class ChessMoveRequestDto {
  // username
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
  readonly username: string;

  // todo: create a validator isfen
  // fen
  @ApiProperty({ required: true })
  @IsString({ message: 'error.invalidFen: Fen must be a string.' })
  @IsNotEmpty({ message: 'error.invalidFen: Fen must not be empty.' })
  @MaxLength(255, {
    message:
      'error.invalidFen: Fen must be shorter than or equal to 255 characters.',
  })
  readonly fen: string;
}
