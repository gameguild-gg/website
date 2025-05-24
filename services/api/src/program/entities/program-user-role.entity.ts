import { Entity, Column, ManyToOne, DeleteDateColumn, JoinColumn } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { IsEnum } from 'class-validator';
import { EntityBase } from '../../common/entities/entity.base';
import { ProgramRoleType } from './enums';
import { Program } from './program.entity';
import { ProgramUser } from './program-user.entity';

@Entity('program_user_roles')
export class ProgramUserRole extends EntityBase {
  @ManyToOne(() => Program, (program) => program.programUserRoles, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_id' })
  @ApiProperty({ description: 'Reference to the program this role applies to' })
  program: Program;

  @ManyToOne(() => ProgramUser, (programUser) => programUser.roles, { onDelete: 'CASCADE' })
  @JoinColumn({ name: 'program_user_id' })
  @ApiProperty({ description: 'Reference to the user whose role is being defined' })
  programUser: ProgramUser;

  @Column({ type: 'enum', enum: ProgramRoleType })
  @ApiProperty({ enum: ProgramRoleType, description: 'Type of role assigned to the user in this program' })
  @IsEnum(ProgramRoleType)
  role: ProgramRoleType;

  @DeleteDateColumn()
  @ApiProperty({ description: 'Soft delete timestamp - when the role was deleted, null if active', required: false })
  deletedAt: Date | null;
}
