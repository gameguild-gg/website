import { Entity, Column, OneToMany, ManyToMany, JoinTable, Index, DeleteDateColumn } from 'typeorm';
import { IsString, IsNotEmpty, IsOptional, IsEnum, IsJSON } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { TagType } from './enums';
import { TagProficiency } from './tag-proficiency.entity';

@Entity('tags')
@Index((entity) => [entity.name, entity.type], { unique: true })
@Index((entity) => [entity.type])
@Index((entity) => [entity.category])
export class Tag extends EntityBase {
  @Column('varchar', { length: 255 })
  @ApiProperty({ description: 'Display name of the tag' })
  @IsString()
  @IsNotEmpty()
  name: string;

  @Column('text', { nullable: true })
  @ApiProperty({ description: 'Detailed description of what this tag represents', required: false })
  @IsString()
  @IsOptional()
  description?: string;

  @Column({ type: 'enum', enum: TagType })
  @ApiProperty({ enum: TagType, description: 'Category type of the tag (skill, topic, technology, etc.)' })
  @IsEnum(TagType)
  type: TagType;

  @Column('varchar', { length: 100, nullable: true })
  @ApiProperty({ description: 'Optional sub-category for organizational purposes', required: false })
  @IsString()
  @IsOptional()
  category?: string;

  @Column('jsonb', { nullable: true })
  @ApiProperty({ description: 'Additional tag-specific metadata and configuration', required: false })
  @IsOptional()
  @IsJSON()
  metadata?: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp', required: false })
  deletedAt: Date | null;

  // Relations
  @OneToMany(() => TagProficiency, (tagProficiency) => tagProficiency.tag)
  tagProficiencies: TagProficiency[];

  // Self-referential many-to-many relationships for tag hierarchy and relationships
  @ManyToMany(() => Tag, (tag) => tag.parentTags, {
    onDelete: 'CASCADE',
    onUpdate: 'CASCADE',
  })
  @JoinTable({
    name: 'tag_relationships',
    joinColumn: {
      name: 'source_id',
      referencedColumnName: 'id',
    },
    inverseJoinColumn: {
      name: 'target_id',
      referencedColumnName: 'id',
    },
  })
  @ApiProperty({
    type: () => Tag,
    isArray: true,
    description: 'Child tags in hierarchical or related relationships',
    required: false,
  })
  childTags: Tag[];

  @ManyToMany(() => Tag, (tag) => tag.childTags)
  @ApiProperty({
    type: () => Tag,
    isArray: true,
    description: 'Parent tags in hierarchical or related relationships',
    required: false,
  })
  parentTags: Tag[];
}
