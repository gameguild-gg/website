import { Entity, ManyToOne, Column, OneToMany } from 'typeorm';
import { EntityBase } from '../../../common/entity.base';
import { UserEntity } from '../../user/user.entity';
import { CompetitionMatchEntity } from './competition.match.entity';
import { ApiProperty } from '@nestjs/swagger';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionRunSubmissionReportEntity } from './competition.run.submission.report.entity';

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
