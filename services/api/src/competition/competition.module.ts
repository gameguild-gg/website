import { forwardRef, Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CompetitionMatchEntity } from './entities/competition.match.entity';
import { CompetitionRunEntity } from './entities/competition.run.entity';
import { CompetitionSubmissionEntity } from './entities/competition.submission.entity';
import { CompetitionController } from './competition.controller';
import { CompetitionService } from './competition.service';
import { UserModule } from '../user/user.module';
import { AuthModule } from '../auth/auth.module';
import { CompetitionRunSubmissionReportEntity } from './entities/competition.run.submission.report.entity';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      CompetitionMatchEntity,
      CompetitionRunEntity,
      CompetitionSubmissionEntity,
      CompetitionRunSubmissionReportEntity,
    ]),
    forwardRef(() => UserModule),
    forwardRef(() => AuthModule),
  ],
  controllers: [CompetitionController],
  exports: [CompetitionService],
  providers: [CompetitionService],
})
export class CompetitionModule {}
