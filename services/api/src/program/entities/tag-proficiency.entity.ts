import { Entity, Column, ManyToOne, JoinColumn, Index, DeleteDateColumn } from 'typeorm';
import { IsString, IsNumber, IsOptional, IsEnum, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { SkillProficiencyLevel } from './enums';
import { Tag } from './tag.entity';

@Entity('tag_proficiencies')
@Index((entity) => [entity.tag, entity.proficiencyLevel], { unique: true })
@Index((entity) => [entity.tag, entity.proficiencyLevelValue], { unique: true })
@Index((entity) => [entity.tag])
@Index((entity) => [entity.proficiencyLevel])
@Index((entity) => [entity.proficiencyLevelValue])
@Index((entity) => [entity.deletedAt])
export class TagProficiency extends EntityBase {
  @ApiProperty({ type: () => Tag, description: 'Tag this proficiency level applies to' })
  @ManyToOne(() => Tag, (tag) => tag.tagProficiencies, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'tag_id' })
  tag: Tag;

  @Column({ type: 'enum', enum: SkillProficiencyLevel })
  @ApiProperty({ enum: SkillProficiencyLevel, description: 'The proficiency level (novice, intermediate, expert, etc.)' })
  @IsEnum(SkillProficiencyLevel)
  proficiencyLevel: SkillProficiencyLevel;

  @Column('float')
  @ApiProperty({ description: 'Numeric value representing the proficiency level, to speedup sorting and filtering' })
  @IsNumber()
  proficiencyLevelValue: number;

  @Column('text', { nullable: true })
  @ApiProperty({ description: 'Description of what this proficiency level means for this specific tag/skill', required: false })
  @IsOptional()
  @IsString()
  description?: string;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Other tag proficiencies required before achieving this level', required: false })
  @IsOptional()
  @IsJSON()
  prerequisites?: object;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Additional proficiency-specific configuration', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp', required: false })
  deletedAt?: Date;
}
