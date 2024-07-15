import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsOptional, IsString } from 'class-validator';
import { EntityDto } from '../dtos/entity.dto';

export class AssetDto extends EntityDto {
  // filename
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  readonly filename: string;

  // mimetype
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  readonly mimetype: string;

  // size
  @ApiProperty()
  @IsNotEmpty()
  @IsOptional()
  readonly sizeBytes: number;

  // url
  @ApiProperty()
  @IsString()
  @IsNotEmpty()
  @IsOptional()
  readonly url: string;
}
