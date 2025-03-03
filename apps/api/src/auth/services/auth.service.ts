import { Injectable, Logger, UnauthorizedException } from '@nestjs/common';
import { CommandBus, QueryBus } from '@nestjs/cqrs';
import { buildUniqueWhereCondition, buildWhereConditions } from '@/common/utils';
import { CreateUserCommand } from '@/user/commands/create-user.command';
import { UserDto } from '@/user/dtos/user.dto';
import { UserAlreadyExistsException } from '@/user/exceptions/user-already-exists.exception';
import { FindOneUserQuery } from '@/user/queries/find-one-user.query';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
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

  // public async validateAccessToken(payload: Readonly<AccessTokenPayloadDto>) {
  //   return undefined;
  // }

  // public async validateRefreshToken(payload: Readonly<RefreshTokenPayload>) {
  //   return undefined;
  // }

  // public async signUpWithEmailAndPassword(data: Readonly<LocalSignUpRequestDto>): Promise<UserDto> {
  //   // TODO: Implement a feature flag to active or deactivate sign-ip/sign-up with email and password.
  //
  //   const { username, email } = data;
  //
  //   return await this.register({ username, email });
  // }

  public async register(data: Readonly<Partial<UserDto>>) {
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

  public async signInWithProvider(data: Readonly<Partial<UserDto>>): Promise<UserDto> {
    // TODO: If is possible to refactor this to a more generic way. using decorators to define the fields to check as providers.
    const findConditions = buildUniqueWhereCondition<UserDto>(data, ['appleId', 'facebookId', 'githubId', 'googleId', 'linkedinId', 'twitterId']);

    try {
      return await this.register(data);
    } catch (error: unknown) {
      if (error instanceof UserAlreadyExistsException) {
        // TODO: Check if the error that user already exists.
        // TODO: must check if the given provider is the same registered in the database.

        return await this.queryBus.execute(new FindOneUserQuery({ where: findConditions }));
      }
      throw error;
    }
  }

  public async validateSignIn(user: Readonly<UserDto>) {
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
