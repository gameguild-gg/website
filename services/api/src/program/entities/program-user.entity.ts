import { Entity, Column, ManyToOne, OneToMany, Index, DeleteDateColumn, JoinColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsJSON } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { UserProduct } from './user-product.entity';
import { Program } from './program.entity';
import { ContentInteraction } from './content-interaction.entity';
import { ProgramUserRole } from './program-user-role.entity';

@Entity('program_users')
@Index((entity) => [entity.userProduct, entity.program], { unique: true, where: 'deleted_at IS NULL' })
@Index((entity) => [entity.program], { where: 'deleted_at IS NULL' })
@Index((entity) => [entity.userProduct], { where: 'deleted_at IS NULL' })
export class ProgramUser extends EntityBase {
  @ManyToOne(() => UserProduct, (userProduct) => userProduct.programUsers, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'user_product_id' })
  @ApiProperty({ description: 'Reference to the purchased product that grants access to this program' })
  userProduct: UserProduct;

  @ManyToOne(() => Program, (program) => program.programUsers, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  @ApiProperty({ type: () => Program, description: 'Reference to the program the user is enrolled in' })
  program: Program;

  @Column({ type: 'jsonb', default: '{}' })
  @ApiProperty({ description: 'Tracks time spent on different parts of the program, last accessed timestamps, etc.' })
  @IsJSON()
  analytics: object;

  @Column({ type: 'jsonb', default: '{}' })
  @ApiProperty({ description: 'Cache of user grades for quick access (quizzes, assignments, overall grade)' })
  @IsJSON()
  grades: object;

  @Column({ type: 'jsonb', default: '{}' })
  @ApiProperty({ description: 'Completion status of program content (pages, activities, etc.)' })
  @IsJSON()
  progress: object;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when enrollment was deleted, null if active', required: false })
  deletedAt: Date | null;

  // Relations
  @OneToMany(() => ContentInteraction, (interaction) => interaction.programUser)
  @ApiProperty({ type: () => ContentInteraction, isArray: true, description: 'User interactions with program content' })
  contentInteractions: ContentInteraction[];

  @OneToMany(() => ProgramUserRole, (programUserRole) => programUserRole.programUser)
  @ApiProperty({ type: () => ProgramUserRole, isArray: true, description: 'Roles assigned to this user in the program' })
  roles: ProgramUserRole[];
}
