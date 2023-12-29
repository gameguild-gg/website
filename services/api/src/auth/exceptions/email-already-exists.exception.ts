import { ConflictException } from '@nestjs/common';

export class EmailAlreadyExistsException extends ConflictException {
  constructor(email: string) {
    super(`Email ${email} already exists`);
  }
}
