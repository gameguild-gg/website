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

  // public async validateAccessToken(payload: Readonly<AccessTokenPayloadDto>) {
  //   return undefined;
  // }

  // public async validateRefreshToken(payload: Readonly<RefreshTokenPayload>) {
  //   return undefined;
  // }

  //   async sendMagicLink(data: EmailDto): Promise<OkDto> {
  //     let user = await this.userService.findOne({
  //       where: { email: data.email },
  //     });
  //
  //     if (!user) user = await this.userService.createOneWithEmail(data.email);
  //
  //     // send email with magic link
  //     const token = await this.generateRefreshToken(user);
  //
  //     const link = `${this.configService.hostFrontendUrl}/connect/?token=${token}`;
  //
  //     // send the email notification
  //     await this.notificationService.sendEmailNotification(
  //       data.email,
  //       'GameGuild Magic Link',
  //       `Use the following link to connect to Game Guild website: <a href="${link}">Connect</a>. The full link is: ${link}`,
  //     );
  //
  //     return { success: true, message: 'Email sent.' };
  //   }

  // public async validateEmailVerificationToken(token: string) {
  //   // TODO: Validate email verification token.
  //   try {
  //     const decodedToken = this.jwtService.verify(token, {
  //       publicKey:
  //         this.configService.authConfig.emailVerificationTokenPublicKey,
  //     });
  //
  //     const user = await this.userService.findOne({
  //       where: { id: decodedToken.sub },
  //     });
  //
  //     user.emailVerified = true;
  //     await this.userService.save(user);
  //   } catch (exception) {
  //     throw new UnauthorizedException('Invalid or expired token');
  //   }
  // }

  //   public async userExists(user: string): Promise<boolean> {
  //     const foundUser = await this.userService.findOne({
  //       where: [{ email: user }, { username: user }],
  //       select: ['id'], // Only select the id to reduce payload size.
  //     });
  //
  //     return Boolean(foundUser);
  //   }

  // public async signUpWithEmailAndPassword(data: Readonly<LocalSignUpRequestDto>): Promise<UserDto> {
  //   // TODO: Implement a feature flag to active or deactivate sign-ip/sign-up with email and password.
  //
  //   const { username, email } = data;
  //
  //   return await this.register({ username, email });
  // }

  //   async generateWeb3SignInChallenge(
  //     data: EthereumSigninChallengeRequestDto,
  //   ): Promise<EthereumSigninChallengeResponseDto> {
  //     // use siwe to generate the message to be signed
  //     const termOfServiceUrl: string = `${this.configService.hostFrontendUrl}/tos`;
  //     const siwe = new SiweMessage({
  //       address: data.address,
  //       domain: new URL(this.configService.hostFrontendUrl).host,
  //       statement: `I accept the GameDev Guild Terms of Service: ${termOfServiceUrl}`,
  //       issuedAt: new Date().toISOString(),
  //       uri: this.configService.hostFrontendUrl,
  //       version: '1',
  //     });
  //
  //     const key = `web3:challenge:message:${data.address}`;
  //
  //     // TODO: It's a must because its what is accepted by the sign-in procedure.
  //     const message = siwe.prepareMessage(); //`0x${Buffer.from(challenge, 'utf8').toString('hex')}`;
  //
  //     // stores it in the cache for 5 minutes
  //     await this.cacheManager.set(key, message, 5 * 60 * 1000);
  //
  //     return { message };
  //   }

  //   async validateWeb3SignInChallenge(
  //     data: EthereumSigninValidateRequestDto,
  //   ): Promise<LocalSignInResponseDto> {
  //     const key = `web3:challenge:message:${data.address}`;
  //
  //     const expectedMessage = await this.cacheManager.get<string>(key);
  //
  //     if (!expectedMessage) {
  //       throw new UnauthorizedException(
  //         'Invalid message challenge. Maybe expired? Try again.',
  //       );
  //     }
  //
  //     let walletAddress: string;
  //     try {
  //       walletAddress = ethers.verifyMessage(expectedMessage, data.signature);
  //     } catch (exception) {
  //       throw new UnauthorizedException('Invalid signature. Please try again.');
  //     }
  //
  //     let user = await this.userService.findOne({
  //       where: { walletAddress: walletAddress },
  //       relations: { profile: true },
  //     });
  //
  //     if (!user) {
  //       user = await this.userService.createOneWithWalletAddress(walletAddress);
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
  //   }

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
