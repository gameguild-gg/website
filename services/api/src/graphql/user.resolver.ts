import { Resolver, Query, Args, ID } from '@nestjs/graphql';
import { ObjectType, Field, Int } from '@nestjs/graphql';
import { UserService } from '../user/user.service';
import { UserEntity } from '../user/entities/user.entity';

@ObjectType()
export class User {
  @Field(() => ID)
  id: string;

  @Field({ nullable: true })
  username?: string;

  @Field({ nullable: true })
  email?: string;

  @Field()
  createdAt: Date;

  @Field()
  updatedAt: Date;

  @Field({ nullable: true })
  emailVerified?: boolean;
}

@Resolver(() => User)
export class UserResolver {
  constructor(private readonly userService: UserService) {}

  @Query(() => [User], { name: 'users' })
  async findAll(): Promise<User[]> {
    // Using find with TypeORM find options
    const users = await this.userService.find({
      take: 10, // Limit to 10 users for demo purposes
      order: { createdAt: 'DESC' }
    });
    return users.map(user => ({
      id: user.id,
      username: user.username,
      email: user.email,
      createdAt: user.createdAt,
      updatedAt: user.updatedAt,
      emailVerified: user.emailVerified,
    }));
  }

  @Query(() => User, { name: 'user', nullable: true })
  async findOne(@Args('id', { type: () => ID }) id: string): Promise<User | null> {
    try {
      const user = await this.userService.findOne({ where: { id } });
      if (!user) return null;
      
      return {
        id: user.id,
        username: user.username,
        email: user.email,
        createdAt: user.createdAt,
        updatedAt: user.updatedAt,
        emailVerified: user.emailVerified,
      };
    } catch (error) {
      return null;
    }
  }

  @Query(() => Int, { name: 'userCount' })
  async userCount(): Promise<number> {
    return await this.userService.count();
  }
}
