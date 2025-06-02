import { UserDto } from '@/user/dtos/user.dto';

export class UserUpdatedEvent {
  constructor(public readonly user: Partial<UserDto>) {}
}
