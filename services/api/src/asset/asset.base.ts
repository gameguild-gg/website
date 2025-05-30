import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsMimeType, IsNotEmpty, IsOptional, IsPositive, IsString, MaxLength } from 'class-validator';
import { Column, Index } from 'typeorm';
import { EntityBase } from '../common/entities/entity.base';
import { IsIntegerNumber } from '../common/decorators/validator.decorator';

export enum AssetSourceType {
  S3 = 'S3',
  IPFS = 'IPFS',
  CLOUDINARY = 'CLOUDNARY',
  EXTERNAL = 'EXTERNAL',
}

// Base asset entity. This should be extended by other entities that need to store assets.
export class AssetBase extends EntityBase {
  
  @ApiProperty()
  @IsNotEmpty()
  @IsEnum(AssetSourceType)
  @Column({ nullable: false })
  @Index({ unique: false })
  readonly source: AssetSourceType;

  
  @ApiProperty()
  @IsNotEmpty()
  @IsString()
  @Column({ nullable: false, default: '' })
  @Index({ unique: false })
  readonly path: string;

  @ApiProperty()
  @IsNotEmpty()
  @IsString()
  @Column({ nullable: false, default: '' })
  @Index({ unique: false })
  readonly originalFilename: string;

  @ApiProperty()
  @IsString({ message: 'error.invalidFilename: Filename must be a string.' })
  @IsNotEmpty({ message: 'error.invalidFilename: Filename must not be empty.' })
  @MaxLength(255, {
    message: 'error.invalidFilename: Filename must be shorter than or equal to 255 characters.',
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

  @ApiProperty()
  @IsNotEmpty()
  @Column({ nullable: false })
  @Index({ unique: false })
  readonly hash: string;
}
