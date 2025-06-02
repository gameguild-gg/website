import { Entity, Column, ManyToOne, JoinColumn, DeleteDateColumn } from 'typeorm';
import { IsEnum, IsOptional } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { TagRelationshipType } from './enums';
import { Tag } from './tag.entity';

@Entity('tag_relationships')
export class TagRelationship extends EntityBase {
  @ApiProperty({ description: 'Type of relationship (prerequisite, related, parent, etc.)' })
  @Column({ type: 'enum', enum: TagRelationshipType })
  @IsEnum(TagRelationshipType)
  type: TagRelationshipType;

  @ApiProperty({ description: 'Additional relationship-specific configuration', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  metadata?: Record<string, any>;

  @ApiProperty({ description: 'Soft delete timestamp - when the relationship was deleted, null if active', required: false })
  @DeleteDateColumn()
  deletedAt?: Date;

  // Relations
  @ApiProperty({ type: () => Tag, description: 'The source tag in the relationship' })
  @ManyToOne(() => Tag, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'source_id' })
  sourceTag: Tag;

  @ApiProperty({ type: () => Tag, description: 'The target tag in the relationship' })
  @ManyToOne(() => Tag, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'target_id' })
  targetTag: Tag;
}
