import { Column, Entity, ManyToOne } from 'typeorm';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { ApiProperty } from '@nestjs/swagger';

export enum CompetitionWinner {
  Player1 = 'Player1',
  Player2 = 'Player2',
}

@Entity()
export class CompetitionMatchEntity extends EntityBase {
  // competition run
  @ManyToOne(
    () => CompetitionRunEntity,
    (competitionRun) => competitionRun.matches,
  )
  @ApiProperty({ type: () => CompetitionRunEntity })
  run: CompetitionRunEntity;

  // cat. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP1)
  @ApiProperty({ type: () => CompetitionSubmissionEntity })
  p1submission: CompetitionSubmissionEntity;

  // cather. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP2)
  @ApiProperty({ type: () => CompetitionSubmissionEntity })
  p2submission: CompetitionSubmissionEntity;

  @Column({ type: 'enum', enum: CompetitionWinner, nullable: true })
  @ApiProperty({ enum: CompetitionWinner })
  winner: CompetitionWinner;

  // cat points
  @Column({ type: 'float' })
  @ApiProperty()
  p1Points: number;

  // catcher points
  @Column({ type: 'float' })
  @ApiProperty()
  p2Points: number;

  @Column({ type: 'integer' })
  @ApiProperty()
  p1Turns: number;

  @Column({ type: 'integer' })
  @ApiProperty()
  p2Turns: number;

  @Column({ type: 'text', nullable: true, default: null })
  @ApiProperty()
  logs: string;

  @Column({ type: 'text', nullable: true, default: null })
  @ApiProperty()
  lastState: string;
}
