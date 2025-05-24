import { Entity, Column, ManyToOne, JoinColumn } from 'typeorm';
import { IsEnum, IsOptional } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';
import { EntityBase } from '../../common/entities/entity.base';
import { Certificate } from './certificate.entity';
import { TagProficiency } from './tag-proficiency.entity';
import { CertificateTagRelationshipType } from './enums';

@Entity('certificate_tags')
export class CertificateTag extends EntityBase {
  @ApiProperty({ description: 'Type of relationship (requirement, recommendation, provides)' })
  @Column('enum', { enum: CertificateTagRelationshipType })
  @IsEnum(CertificateTagRelationshipType)
  type: CertificateTagRelationshipType;

  @ApiProperty({ description: 'Additional tag-specific data like specific competencies, context, etc.', required: false })
  @Column('jsonb', { nullable: true })
  @IsOptional()
  metadata?: Record<string, any>;

  // Relations
  @ApiProperty({ type: () => Certificate, description: 'The certificate associated with this tag relationship' })
  @ManyToOne(() => Certificate, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'certificate_id' })
  certificate: Certificate;

  @ApiProperty({ type: () => TagProficiency, description: 'The tag proficiency level associated with this certificate' })
  @ManyToOne(() => TagProficiency, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'tag_id' })
  tagProficiency: TagProficiency;
}
