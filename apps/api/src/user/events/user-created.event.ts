import { UserDto } from '@/user/dtos/user.dto';

export class UserCreatedEvent {
  constructor(public readonly user: Partial<UserDto>) {}
}
