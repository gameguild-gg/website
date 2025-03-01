import { ConflictException } from '@nestjs/common';

export class UserAlreadyExistsException extends ConflictException {
  constructor(description?: string) {
    super('error.userAlreadyExists', description);
  }
}
