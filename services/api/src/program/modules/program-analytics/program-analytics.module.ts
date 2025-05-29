import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { Program, ProgramUser, ContentInteraction, ProgramRatingEntity, ProgramFeedbackSubmission, ActivityGrade } from '../../entities';

import { ProgramAnalyticsController } from './program-analytics.controller';
import { ProgramAnalyticsService } from './program-analytics.service';

@Module({
  imports: [TypeOrmModule.forFeature([Program, ProgramUser, ContentInteraction, ProgramRatingEntity, ProgramFeedbackSubmission, ActivityGrade])],
  controllers: [ProgramAnalyticsController],
  providers: [ProgramAnalyticsService],
  exports: [ProgramAnalyticsService],
})
export class ProgramAnalyticsModule {}
