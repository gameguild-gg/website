import { WithRolesEntity } from '../../auth/entities/with-roles.entity';
import { ProjectEntity } from './project.entity';
import { Entity, ManyToOne } from 'typeorm';

@Entity('ticket')
export class TicketEntity extends WithRolesEntity {
  @ManyToOne(() => ProjectEntity, (project) => project.tickets)
  project: ProjectEntity;
}
