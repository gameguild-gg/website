import { Column, Entity, ManyToOne } from 'typeorm';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { ApiProperty } from '@nestjs/swagger';
import { IsEnum, IsNotEmpty, IsNumber, IsOptional, IsString, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { IsIntegerNumber } from '../../common/decorators/validator.decorator';

export enum CompetitionWinner {
  Player1 = 'Player1',
  Player2 = 'Player2',
}

@Entity()
export class CompetitionMatchEntity extends EntityBase {
  // competition run
  @ManyToOne(() => CompetitionRunEntity, (competitionRun) => competitionRun.matches)
  @ApiProperty({ type: () => CompetitionRunEntity })
  @ValidateNested()
  @Type(() => CompetitionRunEntity)
  run: CompetitionRunEntity;

  // cat. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP1)
  @ApiProperty({ type: () => CompetitionSubmissionEntity })
  @ValidateNested()
  @Type(() => CompetitionSubmissionEntity)
  p1submission: CompetitionSubmissionEntity;

  // cather. relation with competition submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.matchesAsP2)
  @ApiProperty({ type: () => CompetitionSubmissionEntity })
  @ValidateNested()
  @Type(() => CompetitionSubmissionEntity)
  p2submission: CompetitionSubmissionEntity;

  @Column({
    type: 'enum',
    enum: CompetitionWinner,
    nullable: true,
    default: null,
  })
  @ApiProperty({ enum: CompetitionWinner })
  @IsOptional()
  @IsEnum(CompetitionWinner, {
    message: 'error.IsEnum: winner should be a valid CompetitionWinner',
  })
  winner: CompetitionWinner;

  // cat points
  @Column({ type: 'float', nullable: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p1Points should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: p1Points should be a number' })
  p1Points: number;

  // catcher points
  @Column({ type: 'float', nullable: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p2Points should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: p2Points should be a number' })
  p2Points: number;

  @Column({ type: 'float4', nullable: false, default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p1cpuTime should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: p1cpuTime should be a number' })
  p1cpuTime: number;

  @Column({ type: 'float4', nullable: false, default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p2cpuTime should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: p2cpuTime should be a number' })
  p2cpuTime: number;

  @Column({ type: 'integer', nullable: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p1Turns should not be empty' })
  @IsIntegerNumber({
    message: 'error.IsIntegerNumber: p1Turns should be an integer',
  })
  p1Turns: number;

  @Column({ type: 'integer', nullable: false })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: p2Turns should not be empty' })
  @IsIntegerNumber({
    message: 'error.IsIntegerNumber: p2Turns should be an integer',
  })
  p2Turns: number;

  @Column({ type: 'text', nullable: true, default: null })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: logs should be a string' })
  logs: string;

  @Column({ type: 'text', nullable: true, default: null })
  @ApiProperty()
  @IsOptional()
  @IsString({ message: 'error.IsString: lastState should be a string' })
  lastState: string;
}
