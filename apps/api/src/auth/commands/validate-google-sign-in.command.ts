import { Command } from '@nestjs/cqrs';

import { GoogleSignInPayloadDto } from '@/auth/dtos/google-sign-in-payload.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class ValidateGoogleSignInCommand extends Command<UserDto> {
  protected constructor(public readonly data: GoogleSignInPayloadDto) {
    super();
  }

  public static create(data: GoogleSignInPayloadDto): ValidateGoogleSignInCommand {
    return new ValidateGoogleSignInCommand(data);
  }
}
