import { Column, Index, ManyToOne } from 'typeorm';
import { VisibilityEnum } from './visibility.enum';
import { ApiProperty } from '@nestjs/swagger';
import { ObjectType, Field } from '@nestjs/graphql';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import { IsEnum, IsNotEmpty, IsOptional, IsString, MaxLength } from 'class-validator';
import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
import { CrudValidationGroups } from '@dataui/crud';
import { ImageEntity } from '../../asset';

// todo: move some of these fields to a more basic entity and add abstract classes to specific intents
@ObjectType({ isAbstract: true })
export abstract class ContentBase extends WithRolesEntity {
  @Field()
  @Column({ length: 255, nullable: false, type: 'varchar' })
  @Index({ unique: true })
  @ApiProperty({ required: true })
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  @IsString()
  @MaxLength(255)
  @IsSlug()
  slug: string;

  @Field()
  @Column({ length: 255, nullable: false, type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty({ required: true })
  @IsOptional()
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  @IsString()
  @MaxLength(255)
  title: string;

  @Field({ nullable: true })
  @Column({ length: 1024, nullable: true })
  @ApiProperty({ required: false })
  @IsOptional()
  @IsString()
  @MaxLength(1024)
  summary: string;

  @Field({ nullable: true })
  @Column({ type: 'text', nullable: true })
  @ApiProperty({ required: false, description: 'The body of the content for simple content types' })
  @IsOptional()
  @IsString()
  @MaxLength(1024 * 64)
  body: string;

  @Field(() => String)
  @Index({ unique: false })
  @Column({
    type: 'enum',
    enum: VisibilityEnum,
    default: VisibilityEnum.DRAFT,
    nullable: false,
  })
  @ApiProperty({ enum: VisibilityEnum, required: false })
  @IsOptional()
  @IsEnum(VisibilityEnum)
  visibility: VisibilityEnum;

  // asset image
  @ApiProperty({ type: ImageEntity, required: false })
  @IsOptional()
  // relation to asset
  @ManyToOne(() => ImageEntity)
  thumbnail: ImageEntity;
}
