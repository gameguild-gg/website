import { Injectable } from '@nestjs/common';
import { Prisma, User } from '@prisma/client';
import { PrismaService } from 'src/common/prisma.service';

@Injectable()
export class UsersService {
  constructor(private prismaService: PrismaService) {}

  // This function just saves the information passed on. I'm assuming it receives the password hashed and salted.
  async create(
    user: Pick<
      Prisma.UserCreateInput,
      'email' | 'passwordHash' | 'passwordSalt'
    >,
  ): Promise<User | undefined> {
    try {
      return this.prismaService.user.create({
        data: user,
      });
    } catch (error) {
      throw error;
    }
  }

  async findOne(
    userWhereUniqueInput: Prisma.UserWhereUniqueInput,
  ): Promise<User | undefined> {
    console.log('userWhereUniqueInput: ', userWhereUniqueInput);
    return this.prismaService.user.findUnique({ where: userWhereUniqueInput });
  }
}
