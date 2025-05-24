import { Entity, Column, OneToMany, Index, DeleteDateColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsString, IsNotEmpty, IsOptional, IsNumber, IsJSON } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ProgramContent } from './program-content.entity';
import { ProgramUser } from './program-user.entity';
import { ProgramUserRole } from './program-user-role.entity';
import { ProductProgram } from './product-program.entity';

@Entity('programs')
@Index((entity) => [entity.slug], { unique: true })
@Index((entity) => [entity.createdAt])
@Index((entity) => [entity.tenancyDomains])
@Index((entity) => [entity.cachedEnrollmentCount])
@Index((entity) => [entity.cachedStudentsCompletionRate])
@Index((entity) => [entity.cachedRating])
export class Program extends EntityBase {
  @Column({ type: 'varchar', length: 255, unique: true })
  @ApiProperty({ description: 'URL-friendly identifier for the program, used in program links' })
  @IsNotEmpty()
  @IsString()
  slug: string;

  @Column({ type: 'text' })
  @ApiProperty({ description: 'Brief description of the program for listings and previews' })
  @IsString()
  summary: string;

  @Column({ type: 'jsonb' })
  @ApiProperty({ description: 'Main program content in JSON format, includes description, objectives, etc.' })
  @IsJSON()
  body: object;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Array of allowed email domains, null if not tenancy-fenced', required: false })
  @IsOptional()
  @IsJSON()
  tenancyDomains: string[] | null;

  @Column({ type: 'int', default: 0 })
  @ApiProperty({ description: 'Denormalized count of current enrolled students for quick access' })
  @IsNumber()
  cachedEnrollmentCount: number;

  @Column({ type: 'int', default: 0 })
  @ApiProperty({ description: 'Denormalized count of students who completed the program' })
  @IsNumber()
  cachedCompletionCount: number;

  @Column({ type: 'decimal', precision: 5, scale: 2, default: 0.0 })
  @ApiProperty({ description: 'Precomputed percentage of enrolled students who completed the program' })
  @IsNumber()
  cachedStudentsCompletionRate: number;

  @Column({ type: 'decimal', precision: 3, scale: 2, default: 0.0 })
  @ApiProperty({ description: 'Precomputed average rating of the program' })
  @IsNumber()
  cachedRating: number;

  @Column({ type: 'jsonb', nullable: true })
  @ApiProperty({ description: 'Program type specific data, late penalty etc', required: false })
  @IsOptional()
  @IsJSON()
  metadata: object | null;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the program was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @OneToMany(() => ProgramContent, (programContent) => programContent.program)
  contents: ProgramContent[];

  @OneToMany(() => ProgramUser, (programUser) => programUser.program)
  programUsers: ProgramUser[];

  @OneToMany(() => ProgramUserRole, (programUserRole) => programUserRole.program)
  programUserRoles: ProgramUserRole[];

  @OneToMany(() => ProductProgram, (productProgram) => productProgram.program)
  @ApiProperty({ type: () => ProductProgram, isArray: true, description: 'Product relationships for this program' })
  productPrograms: ProductProgram[];
}
