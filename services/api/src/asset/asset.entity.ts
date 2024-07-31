import { ApiProperty } from '@nestjs/swagger';
import {
  IsMimeType,
  IsNotEmpty,
  IsPositive,
  IsString,
  MaxLength,
} from 'class-validator';
import { Column, Index } from 'typeorm';
import { EntityBase } from '../common/entities/entity.base';
import { IsIntegerNumber } from '../common/decorators/validator.decorator';

export class AssetEntity extends EntityBase {
  // filename
  @ApiProperty()
  @IsString({ message: 'error.invalidFilename: Filename must be a string.' })
  @IsNotEmpty({ message: 'error.invalidFilename: Filename must not be empty.' })
  @MaxLength(255, {
    message:
      'error.invalidFilename: Filename must be shorter than or equal to 255 characters.',
  })
  @Column({ nullable: false, length: 255, default: '' })
  @Index({ unique: false })
  readonly filename: string;

  // mimetype
  @ApiProperty()
  @IsString({ message: 'error.invalidMimetype: Mimetype must be a string.' })
  @IsNotEmpty({ message: 'error.invalidMimetype: Mimetype must not be empty.' })
  @IsMimeType({
    message: 'error.invalidMimetype: Mimetype must be a valid mime type.',
  })
  @Column({ nullable: false, length: 255, default: '' })
  @Index({ unique: false })
  readonly mimetype: string;

  // size
  @ApiProperty()
  @IsNotEmpty({
    message: 'error.invalidSizeBytes: SizeBytes must not be empty.',
  })
  @IsIntegerNumber({
    message: 'error.invalidSizeBytes: SizeBytes must be an integer number.',
  })
  @IsPositive({
    message: 'error.invalidSizeBytes: SizeBytes must be a positive number.',
  })
  @Column({ nullable: false, type: 'integer', default: 0 })
  readonly sizeBytes: number;

  // url
  @ApiProperty()
  @IsNotEmpty({ message: 'error.invalidUrl: Url must not be empty.' })
  @IsString({ message: 'error.invalidUrl: Url must be a string.' })
  @MaxLength(2048, {
    message:
      'error.invalidUrl: Url must be shorter than or equal to 2048 characters.',
  })
  @Column({ nullable: false, length: 2048, default: '' })
  readonly url: string;
}
