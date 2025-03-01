import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { ProposalEntity } from './entities/proposal.entity';

@Injectable()
export class ProposalService extends TypeOrmCrudService<ProposalEntity> {
  private readonly logger = new Logger(ProposalService.name);

  constructor(
    @InjectRepository(ProposalEntity)
    private readonly repository: Repository<ProposalEntity>,
  ) {
    super(repository);
  }
}
