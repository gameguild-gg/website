import { Controller } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { CompetitionService } from './competition.service';

@Controller('Competitions')
@ApiTags('competitions')
export class CompetitionController {
  constructor(public service: CompetitionService) {}
}
