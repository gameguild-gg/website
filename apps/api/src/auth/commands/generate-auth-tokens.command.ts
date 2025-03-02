import { Command } from '@nestjs/cqrs';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class GenerateAuthTokensCommand extends Command<SignInResponseDto> {
  protected constructor(public readonly data: UserDto) {
    super();
  }

  public static create(data: UserDto): GenerateAuthTokensCommand {
    return new GenerateAuthTokensCommand(data);
  }
}
