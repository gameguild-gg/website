import { Injectable, Logger } from '@nestjs/common';
import { Cron } from '@nestjs/schedule';
import { ChoiceCastService } from './choice-cast.service';

@Injectable()
export class ChoiceCastScheduler {
  private readonly logger = new Logger(ChoiceCastScheduler.name);

  constructor(private readonly choiceCastService: ChoiceCastService) {}

  // sunday midnight
  @Cron('0 0 * * 0')
  async flushWeeklyCasts() {
    await this.choiceCastService.flushWeeklyCasts();
  }
  
  // friday 5 pm
  @Cron('0 0 17 * 5')
  async selectWeeklyCasts() {
    await this.choiceCastService.flushWeeklyCasts();
  }
}
