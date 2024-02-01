import { Column, Entity, ManyToOne, OneToMany } from 'typeorm';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import {EntityBase} from "../../common/entities/entity.base";

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
  run: CompetitionRunEntity;

  // cat. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP1)
  p1submission: CompetitionSubmissionEntity;

  // cather. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP2)
  p2submission: CompetitionSubmissionEntity;

  @Column({ type: 'enum', enum: CompetitionWinner, nullable: true })
  winner: CompetitionWinner;

  // cat points
  @Column({ type: 'float' })
  p1Points: number;

  // catcher points
  @Column({ type: 'float' })
  p2Points: number;

  @Column({ type: 'integer' })
  p1Turns: number;

  @Column({ type: 'integer' })
  p2Turns: number;

  @Column({ type: 'text', nullable: true, default: null })
  logs: string;

  @Column({ type: 'text', nullable: true, default: null })
  lastState: string;
}
