import { Injectable, Logger, UnauthorizedException } from '@nestjs/common';
import { JwtService } from "@nestjs/jwt";
import { generateHash, generateRandomSalt, validateHash } from "../common/utils/hash";
import { UserEntity } from "../user/entities/user.entity";
import { UserService } from "../user/user.service";
import { LocalSignInDto, LocalSignUpDto } from "./dtos";

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    private readonly jwtService: JwtService,
    private readonly userService: UserService,
  ) {}

  public async signIn(user: UserEntity) {

    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return {
      user: user,
      accessToken: accessToken,
      refreshToken: refreshToken,
    };
  }

  public async signUpWithEmailAndPassword(data: LocalSignUpDto) {
    const passwordSalt = generateRandomSalt();
    const passwordHash = generateHash(data.password, passwordSalt);

    try {
      const user = await this.userService.createOneWithEmailAndPassword({
        // username: data.username,
        email: data.email,
        passwordHash: passwordHash,
        passwordSalt: passwordSalt
      });

      this.sendEmailVerification(user);

      return user;
    } catch (exception) {
      throw exception;
    }
  }

  public async sendEmailVerification(user: UserEntity) {
    // TODO: Send email verification for the user. 
  }

  public async validateLocalSignIn(data: LocalSignInDto) {
    const { email, password } = data;

    const user = await this.userService.findOne({ where: { email: email } });

    if (!user) {
      throw new UnauthorizedException();
    }

    if (!user.passwordHash || !user.passwordSalt) {
      throw new UnauthorizedException();
    }

    const passwordHash = user.passwordHash;
    const passwordSalt = user.passwordSalt;

    if (!validateHash(password, passwordHash, passwordSalt)) {
      throw new UnauthorizedException();
    }

    // TODO: Check if user is disabled.
    // if (user.isDisabled) {
    //   throw new UnauthorizedException();
    // }

    // TODO: Implement feature toggle for enforce email verification.
    // TODO: Check if user has verified email.
    // if (requireEmailVerification && !user.isEmailVerified) {
    //   throw new UnauthorizedException();
    // }

    return user;
  }

  public async generateAccessToken(user: UserEntity): Promise<any> {
    const payload = {
      sub: user.id,
      email: user.email,
    };

    return this.jwtService.sign(payload);

    //   return {
    //     refresh_token: this.jwtService.sign(
    //       payload,
    //       // TODO: get private key from gcp secret manager.
    //       //   {
    //       //   algorithm: 'RS256',
    //       //   privateKey: process.env.REFRESH_TOKEN_PRIVATE_KEY,
    //       // }
    //     ),
    //   };
  }

  async generateRefreshToken(data: any): Promise<any> {
    //   const payload = {};
    //
    //   return {
    //     refresh_token: this.jwtService.sign(
    //       payload,
    //       // TODO: get private key from gcp secret manager.
    //       //   {
    //       //   algorithm: 'RS256',
    //       //   privateKey: process.env.REFRESH_TOKEN_PRIVATE_KEY,
    //       // }
    //     ),
    //   };
  }

  // async validateRefreshToken(data: any) {
  //   // TODO: Implement refresh token validation.
  // }

  // async revokeRefreshToken(data: any) {
  //   // TODO: Implement refresh token validation.
  // }

  // TODO: Move to auth module because is more related.
  // async markEmailAsVerified(id: string): Promise<UserEntity> {
  //   let user = await this.findOne({ where: { id: id } });
  //
  //   if (user) {
  //     user.emailVerified = true;
  //     return = await this.repository.save(user);
  //
  //     return user;
  //   }
  //
  //   throw new UserNotFoundException();
  // }


}