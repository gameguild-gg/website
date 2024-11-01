import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, OneToMany } from 'typeorm';
import { ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ProjectVersionEntity } from './project-version.entity';
import { TicketEntity } from './ticket.entity';

@Entity('project')
export class ProjectEntity extends ContentBase {
  @ApiProperty({ type: ProjectVersionEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => ProjectVersionEntity)
  @OneToMany(() => ProjectVersionEntity, (version) => version.project)
  versions: ProjectVersionEntity[];

  @ApiProperty({ type: TicketEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => TicketEntity)
  @OneToMany(() => TicketEntity, (ticket) => ticket.project)
  tickets: TicketEntity[];
}
