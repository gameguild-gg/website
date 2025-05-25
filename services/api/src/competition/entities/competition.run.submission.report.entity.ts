// how a given submission performed in a given run

import { Column, Entity, ManyToOne } from 'typeorm';
import { ApiProperty } from '@nestjs/swagger';
import { ObjectType, Field } from '@nestjs/graphql';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import { EntityBase } from '../../common/entities/entity.base';
import { UserEntity } from '../../user/entities';
import { IsArray, IsNotEmpty, IsNumber, ValidateNested } from 'class-validator';
import { Type } from 'class-transformer';
import { IsIntegerNumber } from '../../common/decorators/validator.decorator';

@ObjectType()
@Entity()
export class CompetitionRunSubmissionReportEntity extends EntityBase {
  @Field()
  @Column({ type: 'integer', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: winsAsP1 should not be empty' })
  @IsIntegerNumber({
    message: 'error.IsIntegerNumber: winsAsP1 should be an integer',
  })
  winsAsP1: number;

  @Field()
  @Column({ type: 'integer', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: winsAsP2 should not be empty' })
  @IsIntegerNumber({
    message: 'error.IsIntegerNumber: winsAsP2 should be an integer',
  })
  winsAsP2: number;

  @Field()
  @Column({ type: 'integer', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: totalWins should not be empty' })
  @IsIntegerNumber({
    message: 'error.IsIntegerNumber: totalWins should be an integer',
  })
  totalWins: number;

  @Column({ type: 'float8', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: pointsAsP1 should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: pointsAsP1 should be a number' })
  pointsAsP1: number;

  @Column({ type: 'float8', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: pointsAsP2 should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: pointsAsP2 should be a number' })
  pointsAsP2: number;

  @Column({ type: 'float8', default: 0 })
  @ApiProperty()
  @IsNotEmpty({ message: 'error.IsNotEmpty: totalPoints should not be empty' })
  @IsNumber({}, { message: 'error.IsNumber: totalPoints should be a number' })
  totalPoints: number;

  @ManyToOne(() => CompetitionRunEntity, (c) => c.reports)
  @ApiProperty({ type: () => CompetitionRunEntity })
  @ValidateNested()
  @Type(() => CompetitionRunEntity)
  run: CompetitionRunEntity;

  // link to the submission
  @ManyToOne(() => CompetitionSubmissionEntity, (s) => s.submissionReports)
  @ApiProperty({ type: () => CompetitionSubmissionEntity })
  @ValidateNested()
  @Type(() => CompetitionSubmissionEntity)
  submission: CompetitionSubmissionEntity;

  // link to the user
  @ManyToOne(() => UserEntity)
  @ApiProperty({ type: () => UserEntity })
  @ValidateNested()
  @Type(() => UserEntity)
  user: UserEntity;
}
