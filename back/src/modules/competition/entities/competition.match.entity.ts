import { Column, Entity, ManyToOne, OneToMany } from 'typeorm';
import { CompetitionRunEntity } from './competition.run.entity';
import { UserEntity } from '../../user/user.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import { EntityBase } from '../../../common/entity.base';

export enum CompetitionWinner {
  CAT = 'CAT',
  CATCHER = 'CATCHER',
}

@Entity()
export class CompetitionMatchEntity extends EntityBase {
  // competition run
  @ManyToOne(
    () => CompetitionRunEntity,
    (competitionRun) => competitionRun.matches,
  )
  run: CompetitionRunEntity;

  // cat. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsCat)
  cat: CompetitionSubmissionEntity;

  // cather. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsCatcher)
  catcher: CompetitionSubmissionEntity;

  @Column({ type: 'enum', enum: CompetitionWinner, nullable: true })
  winner: CompetitionWinner;

  // cat points
  @Column({ type: 'float' })
  catPoints: number;

  // catcher points
  @Column({ type: 'float' })
  catcherPoints: number;

  @Column({ type: 'integer' })
  catTurns: number;

  @Column({ type: 'integer' })
  catcherTurns: number;

  @Column({ type: 'text' })
  logs: string;
}
