import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { ChoiceCastEntity } from './entities/choice-cast.entity';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class ChoiceCastService extends TypeOrmCrudService<ChoiceCastEntity> {
  private readonly logger = new Logger(ChoiceCastService.name);
  
  constructor(
    @InjectRepository(ChoiceCastEntity)
    private readonly repository: Repository<ChoiceCastEntity>,
  ) {
    super(repository);
  }

  async flushWeeklyCasts() {
    // return this.eventRepository.delete({ eventType: 'ChoiceCast' });
    return;
  }

  async submitChoiceCast(): Promise<any> {
    return;
  }

  async resetVote(): Promise<any> {
    return;
  }

  async voteForChoiceCast(): Promise<any> {
    return;
  }
  
  async getVotesForChoiceCast(): Promise<any> {
    return;
  }

  async getChoiceCastFromUser(): Promise<ChoiceCastEntity[]> {
    return null;
  }
  
  async getChoiceCastCandidates(): Promise<ChoiceCastEntity[]> {
    return null;
  }
}
