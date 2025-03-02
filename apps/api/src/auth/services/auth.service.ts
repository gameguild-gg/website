import { Inject, Injectable, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { accessTokenConfig as AccessTokenConfig } from '@/auth/config/access-token.config';
import { refreshTokenConfig as RefreshTokenConfig } from '@/auth/config/refresh-token.config';
import { LocalSignInRequest } from '@/auth/dtos/local-sign-in-request.dto';
import { LocalSignUpRequest } from '@/auth/dtos/local-sign-up-request.dto';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    @Inject(AccessTokenConfig.KEY)
    private readonly accessTokenConfig: ConfigType<typeof AccessTokenConfig>,
    @Inject(RefreshTokenConfig.KEY)
    private readonly refreshTokenConfig: ConfigType<typeof RefreshTokenConfig>,
    private readonly jwtService: JwtService,
    // private readonly userService: UserService,
  ) {}

  // TODO: Refactor to CQRS pattern.
  public async signInWithEmailAndPassword(data: Readonly<LocalSignInRequest>) {
    // // TODO: Implement a feature toggle to active or deactivate sign-ip/sign-up with email and password.
    // const { email, password } = data;
    //
    // const user = await this.userService.findOne({ where: { email } });
    //
    // if (!user) {
    //   throw new UnauthorizedException();
    // }
    //
    // if (user.isDisabled) {
    //   throw new UnauthorizedException();
    // }
    //
    // // TODO: Implement feature toggle for enforce email verification.
    // // TODO: Check if user has verified email.
    // // if (isEmailVerificationRequired && !user.isEmailVerified) {
    // //   throw new UnauthorizedException();
    // // }
    //
    // const { passwordHash, passwordSalt } = user;
    //
    // if (!validateHash(password, passwordHash, passwordSalt)) {
    //   throw new UnauthorizedException();
    // }
    //
    // return await this.issueTokens(user);
    //
  }

  // TODO: Refactor to CQRS pattern.
  public async signUpWithEmailAndPassword(data: Readonly<LocalSignUpRequest>) {
    // // TODO: Implement a feature toggle to active or deactivate sign-ip/sign-up with email and password.
    //
    // const { username, email, password } = data;
    //
    // let user = await this.userService.findOne({ where: [{ username }, { email }] });
    //
    // if (user) {
    //   throw new UnauthorizedException();
    // }
    //
    // user = await this.userService.createOne({ username, email, password });
    //
    // return await this.issueTokens(user);
  }

  // TODO: Refactor to CQRS pattern.
  // public async generateAccessToken(data: Readonly<Partial<UserEntity>>): Promise<string> {
  //   const payload = {
  //     sub: data.id,
  //   };
  //
  //   // TODO: Make keys rotative.
  //   // TODO: get private key from gcp secret manager.
  //   const accessToken = this.jwtService.sign(payload, this.accessTokenConfig);
  //
  //   return accessToken;
  // }

  // TODO: Refactor to CQRS pattern.
  // TODO: Type the return object.
  // public async validateAccessToken(payload: Readonly<AccessTokenPayload>) {
  //   return undefined;
  // }

  // TODO: Refactor to CQRS pattern.
  // public async generateRefreshToken(data: Readonly<Partial<UserEntity>>): Promise<string> {
  //   const payload = {
  //     sub: data.id,
  //   };
  //
  //   // TODO: Make keys rotative.
  //   // TODO: get private key from gcp secret manager.
  //   const refreshToken = this.jwtService.sign(payload, this.refreshTokenConfig);
  //
  //   return refreshToken;
  // }

  // TODO: Refactor to CQRS pattern.
  // TODO: Type the return object.
  // public async validateRefreshToken(payload: Readonly<RefreshTokenPayload>) {
  //   return undefined;
  // }

  // TODO: Refactor to CQRS pattern.
  // private async issueTokens(user: UserEntity) {
  //   const accessToken = await this.generateAccessToken(user);
  //   const refreshToken = await this.generateRefreshToken(user);
  //
  //   // TODO: Type the return object.
  //   return { user, accessToken, refreshToken };
  // }

  // TODO: Refactor to CQRS pattern.
  // public async generateEmailVerificationToken(data: Readonly<Partial<UserEntity>>): Promise<string> {
  //   const payload = {
  //     sub: data.id,
  //   };
  //
  //   // TODO: Make keys rotative.
  //   const emailVerificationToken = this.jwtService.sign(payload, {
  //     // TODO: get private key from gcp secret manager.
  //     algorithm: 'RS256',
  //     //   privateKey: process..env.local.EMAIL_VERIFICATION_TOKEN_PRIVATE_KEY,
  //   });
  //
  //   return emailVerificationToken;
  // }
}
