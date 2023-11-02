import { InternalServerErrorException } from '@nestjs/common';

export class ServerMisconfigurationException extends InternalServerErrorException {
  constructor(description?: string) {
    super('error.serverMisconfigured', description);
  }
}
