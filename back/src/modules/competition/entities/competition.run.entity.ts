import { EntityBase } from '../../../common/entity.base';
import { Column, OneToMany } from 'typeorm';
import { CompetitionMatchEntity } from './competition.match.entity';

export class CompetitionRunEntity extends EntityBase {
  @Column({ type: 'boolean', default: false })
  isRunning: boolean;

  @OneToMany(
    () => CompetitionMatchEntity,
    (competitionMatch) => competitionMatch.competitionRun,
  )
  competitionMatches: CompetitionMatchEntity[];
}
