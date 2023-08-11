import { Injectable, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { ethers } from 'ethers';
import { UsersService } from '../users/users.service';

@Injectable()
export class AuthService {
  constructor(
    private usersService: UsersService,
    private jwtService: JwtService,
  ) {}

  async signUpWithEmailAndPassword(data: any) {
    //
    const salt = generateRandomSalt();
    const hash = generateHash(data.password, salt);

    const user = await this.usersService.create(data.email, hash, salt);

    // if (!user) {
    // }

    // send email.

    return user;
    // return this.generateAccessToken(user);
  }

  async signInWithEmailAndPassword(user: any) {
    const payload = { email: user.email, sub: user.userId };
    return {
      access_token: this.jwtService.sign(payload),
    };
  }

  async validateUser(data: { email: string; password: string }): Promise<any> {
    const user = await this.usersService.findByEmail(data.email);
    console.log('user', user);
    if (!user) {
      throw new UnauthorizedException();
    }

    if (!user.passwordHash || !user.passwordSalt) {
      throw new UnauthorizedException();
    }

    const salt = user.passwordSalt;
    const hash = generateHash(data.password, user.passwordSalt);

    const isPAsswordValid = validateHash(data.password, hash, salt);

    if (!isPAsswordValid) {
      throw new UnauthorizedException();
    }

    // return this.generateAccessToken(user);
    return user;
  }

  async generateAccessToken(data: any): Promise<any> {
    const payload = { role: data.role, email: data.email, sub: data.userId };
    return {
      access_token: this.jwtService.sign(payload),
    };
  }
}

//---------------------------------------------------------------------

export function generateHash(data: string, salt?: string): string {
  if (!salt) salt = '';

  return ethers.keccak256(ethers.toUtf8Bytes(data + salt));
}

export function validateHash(
  data: string,
  hash: string,
  salt?: string,
): boolean {
  return generateHash(data, salt) === hash;
}

export function generateRandomSalt(): string {
  return ethers.hexlify(ethers.randomBytes(32));
}
