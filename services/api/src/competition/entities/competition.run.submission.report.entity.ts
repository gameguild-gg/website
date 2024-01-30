// how a given submission performed in a given run

import { Column, Entity, ManyToOne, OneToMany } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import {EntityBase} from "../../common/entities/entity.base";

@Entity()
export class CompetitionRunSubmissionReportEntity extends EntityBase {
  @Column({ type: 'integer' })
  @ApiProperty()
  winsAsP1: number;

  @Column({ type: 'integer' })
  @ApiProperty()
  winsAsP2: number;

  @Column({ type: 'float8' })
  @ApiProperty()
  p1Points: number;

  @Column({ type: 'float8' })
  @ApiProperty()
  p2Points: number;

  @Column({ type: 'float8' })
  @ApiProperty()
  totalPoints: number;

  @ManyToOne(() => CompetitionRunEntity, (c) => c.reports)
  run: CompetitionRunEntity;

  // link to the submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.submissionReports)
  submission: CompetitionSubmissionEntity;
}
