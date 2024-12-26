import { Column, Entity, OneToMany } from 'typeorm';
import { CompetitionMatchEntity } from './competition.match.entity';
import { CompetitionRunSubmissionReportEntity } from './competition.run.submission.report.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { CompetitionGame } from './competition.submission.entity';
import { ApiProperty } from '@nestjs/swagger';

export enum CompetitionRunState {
  NOT_STARTED = 'NOT_STARTED',
  RUNNING = 'RUNNING',
  FINISHED = 'FINISHED',
  FAILED = 'FAILED',
}

@Entity()
export class CompetitionRunEntity extends EntityBase {
  @Column({
    enum: CompetitionRunState,
    default: CompetitionRunState.NOT_STARTED,
  })
  @ApiProperty({ enum: CompetitionRunState })
  state: CompetitionRunState;

  // game type
  @Column({
    enum: CompetitionGame,
  })
  @ApiProperty({ enum: CompetitionGame })
  gameType: CompetitionGame;

  @OneToMany(
    () => CompetitionMatchEntity,
    (competitionMatch) => competitionMatch.run,
  )
  @ApiProperty({ type: () => CompetitionMatchEntity, isArray: true })
  matches: CompetitionMatchEntity[];

  @OneToMany(() => CompetitionRunSubmissionReportEntity, (c) => c.run)
  @ApiProperty({
    type: () => CompetitionRunSubmissionReportEntity,
    isArray: true,
  })
  reports: CompetitionRunSubmissionReportEntity[];
}
