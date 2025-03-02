import { Inject, Injectable, Logger, UnauthorizedException } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import { CommandBus, QueryBus } from '@nestjs/cqrs';
import { buildUniqueWhereCondition, buildWhereConditions } from '@/common/utils';
import { accessTokenConfig as AccessTokenConfig } from '@/auth/config/access-token.config';
import { refreshTokenConfig as RefreshTokenConfig } from '@/auth/config/refresh-token.config';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { TokenType } from '@/auth/dtos/token-type.enum';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { LocalSignUpRequestDto } from '@/auth/dtos/local-sign-up-request.dto';
import { AccessTokenPayloadDto } from '@/auth/dtos/access-token-payload.dto';
import { RefreshTokenPayloadDto } from '@/auth/dtos/refresh-token-payload.dto';
import { validateHash } from '@/auth/utils';
import { UserDto } from '@/user/dtos/user.dto';
import { CreateUserCommand } from '@/user/commands/create-user.command';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';
import { UserAlreadyExistsException } from '@/user/exceptions/user-already-exists.exception';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    @Inject(AccessTokenConfig.KEY)
    private readonly accessTokenConfig: ConfigType<typeof AccessTokenConfig>,
    @Inject(RefreshTokenConfig.KEY)
    private readonly refreshTokenConfig: ConfigType<typeof RefreshTokenConfig>,
    private readonly jwtService: JwtService,
    private readonly commandBus: CommandBus,
    private readonly queryBus: QueryBus,
  ) {}

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

  public async generateAccessToken(user: Readonly<UserDto>): Promise<string> {
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      // email: user.email,
      // username: user.username,
      // wallet: user.walletAddress,
      type: TokenType.AccessToken,
      // TODO: Add more claims.
    };

    // TODO: Make keys rotate.
    return this.jwtService.sign(payload, {
      algorithm: this.accessTokenConfig.signOptions.algorithm,
      expiresIn: this.accessTokenConfig.signOptions.expiresIn,
      privateKey: this.accessTokenConfig.privateKey,
    });
  }

  // public async validateAccessToken(payload: Readonly<AccessTokenPayloadDto>) {
  //   return undefined;
  // }

  public async generateRefreshToken(user: Readonly<UserDto>): Promise<string> {
    // if (!user) throw new UnauthorizedException('User not found');
    const payload: RefreshTokenPayloadDto = {
      sub: user.id,
      // username: user.username, username,
      type: TokenType.RefreshToken,
      // TODO: Add more claims.
    };

    // TODO: Make keys rotative.
    return this.jwtService.sign(payload, {
      algorithm: this.refreshTokenConfig.signOptions.algorithm,
      expiresIn: this.refreshTokenConfig.signOptions.expiresIn,
      privateKey: this.refreshTokenConfig.privateKey,
    });
  }

  // public async validateRefreshToken(payload: Readonly<RefreshTokenPayload>) {
  //   return undefined;
  // }

  public async signInWithEmailAndPassword(data: Readonly<LocalSignInRequestDto>): Promise<UserDto> {
    // TODO: Implement a feature flag to active or deactivate sign-ip/sign-up with email and password.
    const { email, password } = data;

    const user = await this.queryBus.execute(new FindOneUserQuery({ where: { email } }));

    await this.validateSignIn(user);

    const { passwordHash, passwordSalt } = user;

    if (!validateHash(password, passwordHash, passwordSalt)) {
      throw new UnauthorizedException();
    }

    return user;
  }

  public async signUpWithEmailAndPassword(data: Readonly<LocalSignUpRequestDto>): Promise<UserDto> {
    // TODO: Implement a feature flag to active or deactivate sign-ip/sign-up with email and password.

    const { username, email } = data;

    return await this.register({ username, email });
  }

  // public async signInWithFacebook(accessToken: string): Promise<UserDto> {}

  public async signInWithGoogle(idToken: string): Promise<UserDto> {
    // TODO: Implement a feature flag to active or deactivate sign-ip with google.

    return this.signInProvider({ googleId: idToken });

    //     const client = new OAuth2Client(
    //       this.configService.authConfig.googleClientId,
    //     );
    //     let ticket: LoginTicket;
    //     try {
    //       ticket = await client.verifyIdToken({
    //         idToken: idToken,
    //       });
    //     } catch (exception) {
    //       throw new UnauthorizedException(
    //         'Unauthorized: ' + exception,
    //         'Invalid Google ID Token',
    //       );
    //     }
    //     const payload: TokenPayload = ticket.getPayload();

    //     let user = await this.userService.findOne({
    //       where: [{ googleId: payload.sub }, { email: payload.email }],
    //       relations: { profile: true },
    //     });
    //     if (!user) {
    //       user = await this.userService.createOneWithGoogleId(payload);
    //     }
    //
    //     // generate tokens
    //     const accessToken = await this.generateAccessToken(user);
    //     const refreshToken = await this.generateRefreshToken(user);
    //
    //     return {
    //       user,
    //       accessToken,
    //       refreshToken,
    //     };
  }

  private async issueTokens(user: Readonly<UserDto>): Promise<SignInResponseDto> {
    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return SignInResponseDto.create({ accessToken, refreshToken, user });
  }

  private async register(data: Readonly<Partial<UserDto>>) {
    const findConditions = buildWhereConditions<UserDto>(data);

    let user = await this.queryBus.execute(new FindOneUserQuery({ where: findConditions }));

    if (user) {
      // TODO: should we throw an error here.
      throw new UnauthorizedException();
    }

    user = await this.commandBus.execute(new CreateUserCommand(data));

    await this.validateSignIn(user);

    return user;
  }

  private async signInProvider(data: Readonly<Partial<UserDto>>): Promise<UserDto> {
    // TODO: If is possible to refactor this to a more generic way. using decorators to define the fields to check as providers.
    const findConditions = buildUniqueWhereCondition<UserDto>(data, ['appleId', 'facebookId', 'githubId', 'googleId', 'linkedinId', 'twitterId']);

    try {
      return await this.register(data);
    } catch (error: unknown) {
      if (error instanceof UserAlreadyExistsException) {
        // TODO: Check if the error that user already exists.
        const user = await this.queryBus.execute(new FindOneUserQuery({ where: findConditions }));
        // TODO: must check if the given provider is the same registered in the database.
        return user;
      }
      throw error;
    }
  }

  private async validateSignIn(user: Readonly<UserDto>) {
    if (!user) {
      throw new UnauthorizedException();
    }
    // TODO: <!--
    // if (user.isDisabled) {
    //   throw new UnauthorizedException();
    // }

    // TODO: Implement feature flag for enforce email verification.
    // TODO: Check if user has verified email.
    // if (isEmailVerificationRequired && !user.isEmailVerified) {
    //   throw new UnauthorizedException();
    // }
    // TODO: --> to here.
    // TODO: --> to here.
  }
}
