import { Query } from '@nestjs/cqrs';
import { FindOneOptions } from 'typeorm';
import { UserDto } from '@/user/dtos/user.dto';
import { UserEntity } from '@/user/entities/user.entity';

export class FindOneUserQuery extends Query<UserDto> {
  constructor(public readonly options: FindOneOptions<UserEntity>) {
    super();
  }
}
