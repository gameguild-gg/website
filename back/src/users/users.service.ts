import { Injectable } from '@nestjs/common';
import { PrismaService } from 'src/common/prisma.service';

// This should be a real class/interface representing a user entity
export type User = any;

@Injectable()
export class UsersService {
  constructor(private prismaService: PrismaService) {}

  async create(
    email: string,
    passwordHash: string,
    passwordSalt: string,
  ): Promise<User | undefined> {
    return this.prismaService.user.create({
      data: {
        email: email,
        passwordHash: passwordHash,
        passwordSalt: passwordSalt,
      },
      // select: {
      //   id: true,
      //   email: true,
      //   // passwordHash: true,
      //   // passwordSalt: true,
      // },
    });
  }

  async findByEmail(email: string): Promise<User | undefined> {
    return this.prismaService.user.findUnique({
      where: { email: email },
    });
  }
}
