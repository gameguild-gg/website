import { Column, ManyToOne, OneToMany } from 'typeorm';
import { CompetitionRunEntity } from './competition.run.entity';
import { UserEntity } from '../../user/user.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';

export class CompetitionMatchEntity {
  // competition run
  @ManyToOne(
    () => CompetitionRunEntity,
    (competitionRun) => competitionRun.competitionMatches,
  )
  competitionRun: CompetitionRunEntity;

  // cather. relation with competition submission
  @ManyToOne(() => UserEntity, (user) => user.competitionSubmissions)
  catcher: CompetitionSubmissionEntity;

  // cat. relation with competition submission
  @ManyToOne(() => UserEntity, (user) => user.competitionSubmissions)
  cat: CompetitionSubmissionEntity;

  // cat points
  @Column({ type: 'float' })
  catPoints: number;

  // catcher points
  @Column({ type: 'float' })
  catcherPoints: number;
}
