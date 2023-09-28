import { Module } from '@nestjs/common';
import { TypeOrmModule } from '@nestjs/typeorm';
import { CompetitionMatchEntity } from './competition.match.entity';
import { CompetitionRunEntity } from './competition.run.entity';
import { CompetitionSubmissionEntity } from './competition.submission.entity';
import { CompetitionController } from './competition.controller';
import { CompetitionService } from './competition.service';
import { UserModule } from '../user/user.module';

@Module({
  imports: [
    TypeOrmModule.forFeature([
      CompetitionMatchEntity,
      CompetitionRunEntity,
      CompetitionSubmissionEntity,
    ]),
    UserModule,
  ],
  controllers: [CompetitionController],
  exports: [CompetitionService],
  providers: [CompetitionService],
})
export class ProposalModule {}
