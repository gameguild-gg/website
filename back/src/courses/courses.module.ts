import { Module } from '@nestjs/common';
import { CoursesService } from './courses.service';
import { CoursesController } from './courses.controller';

@Module({
  providers: [CoursesService],
  controllers: [CoursesController]
})
export class CoursesModule {}
