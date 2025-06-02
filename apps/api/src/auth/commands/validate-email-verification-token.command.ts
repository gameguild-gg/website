import { Command } from '@nestjs/cqrs';

import { EmailVerificationTokenPayloadDto } from '@/auth/dtos/email-verification-token-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateEmailVerificationTokenCommand extends Command<UserDto> {
  protected constructor(public readonly data: EmailVerificationTokenPayloadDto) {
    super();
  }

  public static create(data: EmailVerificationTokenPayloadDto): ValidateEmailVerificationTokenCommand {
    return new ValidateEmailVerificationTokenCommand(data);
  }
}
