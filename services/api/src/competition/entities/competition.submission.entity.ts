import { Entity, ManyToOne, Column, OneToMany } from 'typeorm';
import { CompetitionMatchEntity } from './competition.match.entity';

import { CompetitionRunSubmissionReportEntity } from './competition.run.submission.report.entity';
import {EntityBase} from "../../common/entities/entity.base";
import {UserEntity} from "../../user/entities";

@Entity()
export class CompetitionSubmissionEntity extends EntityBase {
  // relation with user
  @ManyToOne(() => UserEntity, (user) => user.competitionSubmissions)
  user: UserEntity;

  // zip file with the source code
  @Column({ type: 'bytea', nullable: false })
  sourceCodeZip: Uint8Array;

  @OneToMany(() => CompetitionMatchEntity, (m) => m.cat)
  matchesAsCat: CompetitionMatchEntity[];

  @OneToMany(() => CompetitionMatchEntity, (m) => m.catcher)
  matchesAsCatcher: CompetitionMatchEntity[];

  // link to reports in runs
  @OneToMany(() => CompetitionRunSubmissionReportEntity, (r) => r.submission)
  submissionReports: CompetitionRunSubmissionReportEntity[];
}
