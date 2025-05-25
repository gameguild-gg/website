import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { Int } from '@nestjs/graphql';
import { CompetitionSubmissionEntity } from '../competition/entities/competition.submission.entity';
import { CompetitionMatchEntity } from '../competition/entities/competition.match.entity';
import { CompetitionRunEntity } from '../competition/entities/competition.run.entity';

@Resolver(() => CompetitionSubmissionEntity)
export class CompetitionResolver {
  @Query(() => [CompetitionSubmissionEntity], { name: 'competitionSubmissions' })
  async findAllSubmissions(): Promise<CompetitionSubmissionEntity[]> {
    // TODO: Implement competition service when available
    return [];
  }

  @Query(() => CompetitionSubmissionEntity, { name: 'competitionSubmission', nullable: true })
  async findOneSubmission(@Args('id', { type: () => ID }) id: string): Promise<CompetitionSubmissionEntity | null> {
    // TODO: Implement competition service when available
    return null;
  }

  @Query(() => [CompetitionMatchEntity], { name: 'competitionMatches' })
  async findAllMatches(): Promise<CompetitionMatchEntity[]> {
    // TODO: Implement competition service when available
    return [];
  }

  @Query(() => [CompetitionRunEntity], { name: 'competitionRuns' })
  async findAllRuns(): Promise<CompetitionRunEntity[]> {
    // TODO: Implement competition service when available
    return [];
  }
}
