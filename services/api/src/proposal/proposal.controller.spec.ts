import { Test, TestingModule } from '@nestjs/testing';
import { ProposalController } from './proposal.controller';

describe('ProposalController', () => {
  let controller: ProposalController;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      controllers: [ProposalController],
    }).compile();

    controller = module.get<ProposalController>(ProposalController);
  });

  it('should be defined', () => {
    expect(controller).toBeDefined();
  });
});
