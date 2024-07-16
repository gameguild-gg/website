import { Logger } from '@nestjs/common';

export class GameService {
  private readonly logger = new Logger(GameService.name);
  constructor() {
    this.logger = new Logger(GameService.name);
  }
}
