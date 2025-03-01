import { Entity, ManyToOne, Column, OneToMany } from 'typeorm';
import { CompetitionMatchEntity } from './competition.match.entity';

import { CompetitionRunSubmissionReportEntity } from './competition.run.submission.report.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities';

export enum CompetitionGame {
  CatchTheCat = 'CatchTheCat',
  Chess = 'Chess',
}

@Entity()
export class CompetitionSubmissionEntity extends EntityBase {
  // relation with user
  @ManyToOne(() => UserEntity, (user) => user.competitionSubmissions)
  user: UserEntity;

  // zip file with the source code
  @Column({ type: 'bytea', nullable: false })
  sourceCodeZip: Uint8Array;

  // executable file staticly compiled without any dependencies
  @Column({ type: 'bytea', nullable: true, default: null })
  executable: Uint8Array;

  @Column({ type: 'enum', enum: CompetitionGame, nullable: false })
  gameType: CompetitionGame;

  @OneToMany(() => CompetitionMatchEntity, (m) => m.p1submission)
  matchesAsP1: CompetitionMatchEntity[];

  @OneToMany(() => CompetitionMatchEntity, (m) => m.p2submission)
  matchesAsP2: CompetitionMatchEntity[];

  // link to reports in runs
  @OneToMany(() => CompetitionRunSubmissionReportEntity, (r) => r.submission)
  submissionReports: CompetitionRunSubmissionReportEntity[];
}
