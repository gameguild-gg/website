import { Column, Entity, Index } from 'typeorm';
import { AssetEntity } from './asset.entity';
import { ApiProperty } from '@nestjs/swagger';
import { IsOptional, IsString } from 'class-validator';

@Entity()
@Index('pathUniqueness', ['path', 'source'], { unique: true })
export class ImageEntity extends AssetEntity {
  readonly width: number;
  readonly height: number;

  // to be used on the frontend to display the asset as alt tag
  @ApiProperty()
  @IsOptional()
  @IsString()
  @Column({ nullable: true, default: null })
  readonly description: string;
}
