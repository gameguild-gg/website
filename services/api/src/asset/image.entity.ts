import { Column, Entity, Index } from 'typeorm';
import { ObjectType, Field } from '@nestjs/graphql';
import { AssetBase } from './asset.base';
import { ApiProperty } from '@nestjs/swagger';
import { IsNotEmpty, IsOptional, IsString } from 'class-validator';
import { IsIntegerNumber } from '../common/decorators/validator.decorator';

@ObjectType()
@Entity({ name: 'images' })
@Index('pathUniqueness', ['path', 'filename', 'source'], { unique: true })
export class ImageEntity extends AssetBase {
  @Field({ nullable: true })
  @ApiProperty({ type: 'integer' })
  @IsOptional()
  @IsIntegerNumber()
  @Column({ nullable: true, type: 'integer' })
  @Index({ unique: false })
  readonly width: number;

  @Field({ nullable: true })
  @ApiProperty({ type: 'integer' })
  @IsOptional()
  @IsIntegerNumber()
  @Column({ nullable: true, type: 'integer' })
  @Index({ unique: false })
  readonly height: number;

  @Field({ nullable: true })
  @ApiProperty()
  @IsOptional()
  @IsString()
  @Column({ nullable: true, default: null })
  readonly description: string;
}
