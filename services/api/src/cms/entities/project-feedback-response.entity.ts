import { EntityBase } from '../../common/entities/entity.base';
import { ProjectVersionEntity } from './project-version.entity';
import { Column, Entity, ManyToOne } from 'typeorm';
import { UserEntity } from '../../user/entities';
import { ApiProperty } from '@nestjs/swagger';
import { IsArray, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';

@Entity('project_feedback_response')
export class ProjectFeedbackResponseEntity extends EntityBase {
  // relationship to the game version
  @ManyToOne(() => ProjectVersionEntity, (version) => version.responses)
  @ApiProperty({ type: () => ProjectVersionEntity })
  @ValidateNested()
  @Type(() => ProjectVersionEntity)
  version: ProjectVersionEntity;

  // relationship to the user
  @ManyToOne(() => UserEntity)
  @ApiProperty({ type: () => UserEntity })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;

  // feedback responses
  @ApiProperty()
  @IsArray()
  @Column({ type: 'jsonb', nullable: false })
  responses: [string | string[] | number];
}
