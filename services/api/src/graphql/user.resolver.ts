import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { Int } from '@nestjs/graphql';
import { UserService } from '../user/user.service';
import { UserEntity } from '../user/entities/user.entity';

@Resolver(() => UserEntity)
export class UserResolver {
  constructor(private readonly userService: UserService) {}

  @Query(() => [UserEntity], { name: 'users' })
  async findAll(): Promise<UserEntity[]> {
    // Using find with TypeORM find options
    const users = await this.userService.find({
      take: 10, // Limit to 10 users for demo purposes
      order: { createdAt: 'DESC' },
    });
    return users;
  }

  @Query(() => UserEntity, { name: 'user', nullable: true })
  async findOne(@Args('id', { type: () => ID }) id: string): Promise<UserEntity | null> {
    try {
      const user = await this.userService.findOne({ where: { id } });
      return user || null;
    } catch (error) {
      return null;
    }
  }

  @Query(() => Int, { name: 'userCount' })
  async userCount(): Promise<number> {
    return await this.userService.count();
  }
}
