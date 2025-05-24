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
import { UserProfileModule } from '../user/modules/user-profile/user-profile.module';

// todo: tolsta split this module into smaller services / controllers. the module can stay as is.

@Module({
  imports: [
    TypeOrmModule.forFeature([CompetitionMatchEntity, CompetitionRunEntity, CompetitionSubmissionEntity, CompetitionRunSubmissionReportEntity]),
    forwardRef(() => UserModule),
    forwardRef(() => AuthModule),
    forwardRef(() => UserProfileModule),
  ],
  controllers: [CompetitionController],
  exports: [CompetitionService],
  providers: [CompetitionService],
})
export class CompetitionModule {}
