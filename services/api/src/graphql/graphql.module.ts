import { Module } from '@nestjs/common';
import { HelloResolver } from './hello.resolver';
import { UserResolver } from './user.resolver';
import { ProgramResolver } from './program.resolver';
import { CourseResolver } from './course.resolver';
import { CompetitionResolver } from './competition.resolver';
import { JobResolver } from './job.resolver';
import { UserModule } from '../user/user.module';

@Module({
  imports: [UserModule],
  providers: [HelloResolver, UserResolver, ProgramResolver, CourseResolver, CompetitionResolver, JobResolver],
})
export class GraphqlModule {}
