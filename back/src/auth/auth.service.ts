import { Injectable, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { ethers } from 'ethers';
import { UsersService } from '../users/users.service';
import { UserEmailAndPassword } from './interfaces/UserEmailAndPassword.interface';
import { HashAndSalt } from './interfaces/HashAndSalt.interface';
import { Prisma, User } from '@prisma/client';

@Injectable()
export class AuthService {
  constructor(
    private usersService: UsersService,
    private jwtService: JwtService,
  ) {}

  public async signUpWithEmailAndPassword(data: UserEmailAndPassword) {
    const salt = this.generateRandomSalt();

    const keys: HashAndSalt = {
      data: data.password,
      salt: salt,
    };

    const hash = this.generateHash(keys);

    const userCreationData: Pick<
      Prisma.UserCreateInput,
      'email' | 'passwordHash' | 'passwordSalt'
    > = {
      email: data.email,
      passwordHash: hash,
      passwordSalt: salt,
    };

    const user = await this.usersService.create(userCreationData);

    // Still needs to create a method to send email verification.

    return user;
  }

  async signJwt(data: UserEmailAndPassword): Promise<any> {
    return;
  }

  async validateUser(
    data: UserEmailAndPassword,
  ): Promise<Omit<User, 'passwordHash' | 'passwordSalt'>> {
    const user = await this.usersService.findOne({ email: data.email });

    // I'm not sure if we really need to check if the passwordHash and passwordSalt exist. Once the user is created, they should always exist.
    // Check later.
    if (user && user.passwordHash && user.passwordSalt) {
      const salt = user.passwordSalt;
      const hash = user.passwordHash;

      const isPAsswordValid = this.validateHash(data.password, {
        data: hash,
        salt,
      });

      if (isPAsswordValid) {
        const { passwordHash, passwordSalt, ...result } = user;
        return result;
      }
    }

    throw new UnauthorizedException();
  }

  private generateHash(keys: HashAndSalt): string {
    const { data, salt } = keys;
    salt ? salt : '';
    return ethers.id(salt + data);
  }

  private validateHash(hash: string, keys: HashAndSalt): boolean {
    return this.generateHash({ data: keys.data, salt: keys.salt }) === hash;
  }

  private generateRandomSalt(): string {
    return ethers.hexlify(ethers.randomBytes(32));
  }
}
