import { ApiProperty } from '@nestjs/swagger';
import {
  IsAlphanumeric,
  IsNotEmpty,
  IsNumber,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';

// todo: move this to a package to be shared between services/api and services/match
export class MatchSearchRequestDto {
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
  username?: string;

  // pageSize
  @ApiProperty({ required: true, default: 100 })
  @IsNotEmpty({ message: 'error.invalidPageSize: PageSize must not be empty.' })
  @IsNumber(
    {},
    { message: 'error.invalidPageSize: PageSize must be a number.' },
  )
  @Min(1, {
    message:
      'error.invalidPageSize: PageSize must be greater than or equal to 1.',
  })
  @Max(100, {
    message:
      'error.invalidPageSize: PageSize must be less than or equal to 100.',
  })
  pageSize: number = 100;

  // pageId
  @ApiProperty({ required: true, default: 0 })
  @IsNotEmpty({ message: 'error.invalidPageId: PageId must not be empty.' })
  @IsNumber({}, { message: 'error.invalidPageId: PageId must be a number.' })
  @Min(0, {
    message: 'error.invalidPageId: PageId must be greater than or equal to 0.',
  })
  pageId: number = 0;
}
