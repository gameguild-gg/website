import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Column, Entity, OneToMany, PrimaryGeneratedColumn } from 'typeorm';
import { IsNotEmpty, IsString, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ProjectVersionEntity } from './project-version.entity';
import { TicketEntity } from './ticket.entity';

@Entity('project')
export class ProjectEntity extends ContentBase {
  @PrimaryGeneratedColumn('uuid')
  id: string;

  @ApiProperty({ type: Number, description: 'Title of the ticket' })
  @Column({ type: 'varchar', length: 255 })
  @IsString()
  @IsNotEmpty()
  Description: string;

  @ApiProperty({ type: ProjectVersionEntity, isArray: true })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => ProjectVersionEntity)
  // relation to ProjectVersionEntity
  @OneToMany(() => ProjectVersionEntity, (version) => version.project)
  versions: ProjectVersionEntity[];

  @OneToMany(() => TicketEntity, (ticket) => ticket.project)
  tickets: TicketEntity[];

  constructor(partial?: Partial<ProjectEntity>) {
    super(partial);
    Object.assign(this, partial);
  }
}
