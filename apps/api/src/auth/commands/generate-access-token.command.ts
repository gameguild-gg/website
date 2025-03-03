import { Command } from '@nestjs/cqrs';
import { UserDto } from '@/user/dtos/user.dto';

export class GenerateAccessTokenCommand extends Command<string> {
  protected constructor(public readonly user: UserDto) {
    super();
  }

  public static create(user: UserDto): GenerateAccessTokenCommand {
    return new GenerateAccessTokenCommand(user);
  }
}
