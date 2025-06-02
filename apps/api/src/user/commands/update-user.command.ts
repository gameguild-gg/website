import { UpdateUserDto } from '@/user/dtos/update-user.dto';

export class UpdateUserCommand {
  constructor(public readonly data: UpdateUserDto) {}
}
