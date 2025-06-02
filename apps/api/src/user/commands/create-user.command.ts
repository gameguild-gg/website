import { Command } from '@nestjs/cqrs';

import { CreateUserDto } from '@/user/dtos/create-user.dto';
import { UserDto } from '@/user/dtos/user.dto';

export class CreateUserCommand extends Command<UserDto> {
  constructor(public readonly data: CreateUserDto) {
    super();
  }
}
