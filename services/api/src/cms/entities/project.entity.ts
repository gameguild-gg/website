import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, OneToMany } from 'typeorm';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ProjectVersionEntity } from './project-version.entity';

@Entity('project')
export class ProjectEntity extends ContentBase {
  @ApiProperty({ type: ProjectVersionEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => ProjectVersionEntity)
  // relation to ProjectVersionEntity
  @OneToMany(() => ProjectVersionEntity, (version) => version.project)
  versions: ProjectVersionEntity[];

  constructor(partial?: Partial<ProjectEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
