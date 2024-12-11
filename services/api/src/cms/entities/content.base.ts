import { Column, Index } from 'typeorm';
import { VisibilityEnum } from './visibility.enum';
import { ApiProperty } from '@nestjs/swagger';
import { IsSlug } from '../../common/decorators/isslug.decorator';
import {
  IsEnum,
  IsNotEmpty,
  IsOptional,
  IsString,
  IsUrl,
  MaxLength,
} from 'class-validator';
import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
import { CrudValidationGroups } from '@dataui/crud';

// todo: move some of these fields to a more basic entity and add abstract classes to specific intents
export abstract class ContentBase extends WithRolesEntity {
  @Column({ length: 255, nullable: false, type: 'varchar' })
  @Index({ unique: true })
  @ApiProperty()
  @IsOptional({ groups: [CrudValidationGroups.UPDATE] })
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  @IsString()
  @MaxLength(255)
  @IsSlug()
  slug: string;

  @Column({ length: 255, nullable: false, type: 'varchar' })
  @Index({ unique: false })
  @ApiProperty()
  @IsOptional()
  @IsNotEmpty({ groups: [CrudValidationGroups.CREATE] })
  @IsString()
  @MaxLength(255)
  title: string;

  @Column({ length: 1024, nullable: true })
  @ApiProperty()
  @IsOptional()
  @IsString()
  @MaxLength(1024)
  summary: string;

  @Column({ type: 'text', nullable: true })
  @ApiProperty()
  @IsOptional()
  @IsString()
  @MaxLength(1024 * 64)
  body: string;

  @Index({ unique: false })
  @Column({
    type: 'enum',
    enum: VisibilityEnum,
    default: VisibilityEnum.DRAFT,
    nullable: false,
  })
  @ApiProperty({ enum: VisibilityEnum })
  @IsOptional()
  @IsEnum(VisibilityEnum)
  visibility: VisibilityEnum;

  // asset image
  @Column({ nullable: true, default: '', type: 'varchar', length: 1024 })
  @ApiProperty()
  @IsOptional()
  @IsUrl({ require_protocol: true })
  thumbnail?: string;
}
