import { EntityBase } from '../../../common/entity.base';
import { Column, Entity, OneToMany } from "typeorm";
import { CompetitionMatchEntity } from './competition.match.entity';

export enum CompetitionRunState {
  NOT_STARTED = 'NOT_STARTED',
  RUNNING = 'RUNNING',
  FINISHED = 'FINISHED',
  FAILED = 'FAILED',
}

@Entity()
export class CompetitionRunEntity extends EntityBase {
  @Column({ enum: CompetitionRunState, default: CompetitionRunState.NOT_STARTED })
  state: CompetitionRunState;

  @OneToMany(
    () => CompetitionMatchEntity,
    (competitionMatch) => competitionMatch.competitionRun,
  )
  competitionMatches: CompetitionMatchEntity[];
}
