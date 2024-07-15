import { AssetDto } from './asset.dto';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsOptional, IsString } from 'class-validator';
import { Column } from 'typeorm';
import { EntityBase } from '../common/entities/entity.base';

export class AssetEntity extends EntityBase implements AssetDto {
  // filename
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  @Column()
  readonly filename: string;

  // mimetype
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  @Column()
  readonly mimetype: string;

  // size
  @ApiProperty()
  @IsNotEmpty()
  @IsOptional()
  @Column()
  readonly sizeBytes: number;

  // url
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  @Column()
  readonly url: string;
}
