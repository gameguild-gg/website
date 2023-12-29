import { Test, TestingModule } from '@nestjs/testing';
import { PhoneService } from './phone.service';

describe('PhoneService', () => {
  let service: PhoneService;

  beforeEach(async () => {
    const module: TestingModule = await Test.createTestingModule({
      providers: [PhoneService],
    }).compile();

    service = module.get<PhoneService>(PhoneService);
  });

  it('should be defined', () => {
    expect(service).toBeDefined();
  });
});
