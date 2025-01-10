import { ContentBase } from './content.base';
import { ApiProperty } from '@nestjs/swagger';
import { Entity, JoinTable, ManyToMany, ManyToOne, OneToMany } from 'typeorm';
import { IsOptional, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { ProjectVersionEntity } from './project-version.entity';
import { TicketEntity } from './ticket.entity';
import { ImageEntity } from '../../asset';

@Entity('project')
export class ProjectEntity extends ContentBase {
  @ApiProperty({ type: ProjectVersionEntity, isArray: true, required: false })
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => ProjectVersionEntity)
  @OneToMany(() => ProjectVersionEntity, (version) => version.project)
  versions: ProjectVersionEntity[];

  @ApiProperty({ type: TicketEntity, isArray: true, required: false })
  @IsOptional()
  @ValidateNested({
    each: true,
    message: 'versions must be an array of GameVersionDto',
  })
  @Type(() => TicketEntity)
  @OneToMany(() => TicketEntity, (ticket) => ticket.project)
  tickets: TicketEntity[];

  @ApiProperty({ type: ImageEntity, required: false })
  @IsOptional()
  // relation to asset
  @ManyToOne(() => ImageEntity, { onDelete: 'CASCADE' })
  banner: ImageEntity;

  @ApiProperty({ type: ImageEntity, isArray: true, required: false })
  @IsOptional()
  @ManyToMany(() => ImageEntity, { onDelete: 'CASCADE' })
  @JoinTable()
  screenshots: ImageEntity[];
}
