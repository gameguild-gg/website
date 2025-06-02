import { NotFoundException } from '@nestjs/common';

export class UserNotFoundException extends NotFoundException {
  constructor(description?: string) {
    super('error.userNotFound', description);
  }
}
