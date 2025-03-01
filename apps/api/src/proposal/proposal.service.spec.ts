import { Test, TestingModule } from '@nestjs/testing';
import { ProposalService } from './proposal.service';

describe('ProposalService', () => {
  let service: ProposalService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      providers: [ProposalService],
    }).compile();

    service = module.get<ProposalService>(ProposalService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });
});
