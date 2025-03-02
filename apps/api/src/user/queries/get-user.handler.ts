import { IQueryHandler, QueryHandler } from '@nestjs/cqrs';
import { GetUserQuery } from '@/user/queries/get-user.query';
import { Logger } from '@nestjs/common';

@QueryHandler(GetUserQuery)
export class GetUserHandler implements IQueryHandler<GetUserQuery> {
  private readonly logger = new Logger(GetUserHandler.name);

  constructor() {}

  async execute(query: GetUserQuery) {
    // return this.userRepository.findOne(query.userId);
  }
}
