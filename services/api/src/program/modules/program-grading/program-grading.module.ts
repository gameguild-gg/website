import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { ActivityGrade, ContentInteraction, ProgramUser, ProgramContent } from '../../entities';

import { ProgramGradingController } from './program-grading.controller';
import { ProgramGradingService } from './program-grading.service';

@Module({
  imports: [TypeOrmModule.forFeature([ActivityGrade, ContentInteraction, ProgramUser, ProgramContent])],
  controllers: [ProgramGradingController],
  providers: [ProgramGradingService],
  exports: [ProgramGradingService],
})
export class ProgramGradingModule {}
