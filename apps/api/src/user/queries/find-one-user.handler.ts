import { Logger } from '@nestjs/common';
import { IQueryHandler, QueryHandler } from '@nestjs/cqrs';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';
import { UserDto } from '@/user/dtos/user.dto';
import { UserService } from '@/user/services/user.service';

@QueryHandler(FindOneUserQuery)
export class FindOneUserHandler implements IQueryHandler<FindOneUserQuery> {
  private readonly logger = new Logger(FindOneUserHandler.name);

  constructor(private readonly userService: UserService) {}

  public async execute(query: FindOneUserQuery): Promise<UserDto> {
    const { options } = query;
    return this.userService.findOne(options);
  }
}
