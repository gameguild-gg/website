import { Module, forwardRef } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { ActivityGrade, ContentInteraction, ProgramContent, Program, ProgramUser } from '../../entities';
import { ProgramModule } from '../../program.module';
import { ProgramContentController } from './program-content.controller';
import { ProgramContentService } from './program-content.service';

@Module({
  imports: [TypeOrmModule.forFeature([ProgramContent, ContentInteraction, ActivityGrade, Program, ProgramUser]), forwardRef(() => ProgramModule)],
  controllers: [ProgramContentController],
  providers: [ProgramContentService],
  exports: [ProgramContentService],
})
export class ProgramContentModule {}
