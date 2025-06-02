import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { ContentInteraction, ProgramUser, ProgramContent, Program } from '../../entities';

import { ProgramProgressController } from './program-progress.controller';
import { ProgramProgressService } from './program-progress.service';

@Module({
  imports: [TypeOrmModule.forFeature([ContentInteraction, ProgramUser, ProgramContent, Program])],
  controllers: [ProgramProgressController],
  providers: [ProgramProgressService],
  exports: [ProgramProgressService],
})
export class ProgramProgressModule {}
