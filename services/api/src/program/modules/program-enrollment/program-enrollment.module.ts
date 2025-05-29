import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';

import { ProgramUser, ProgramUserRole, Program, UserProduct } from '../../entities';

import { ProgramEnrollmentController } from './program-enrollment.controller';
import { ProgramEnrollmentService } from './program-enrollment.service';

@Module({
  imports: [TypeOrmModule.forFeature([ProgramUser, ProgramUserRole, Program, UserProduct])],
  controllers: [ProgramEnrollmentController],
  providers: [ProgramEnrollmentService],
  exports: [ProgramEnrollmentService],
})
export class ProgramEnrollmentModule {}
