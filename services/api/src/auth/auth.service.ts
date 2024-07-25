import {
  ClassSerializerInterceptor,
  Inject,
  Injectable,
  Logger,
  SerializeOptions,
  UnauthorizedException,
  UseInterceptors,
} from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { ApiConfigService } from '../common/config.service';
import {
  generateHash,
  generateRandomSalt,
  validateHash,
} from '../common/utils/hash';
import { NotificationService } from '../notification/notification.service';
import { UserEntity } from '../user/entities';
import { UserService } from '../user/user.service';
import { LocalSignUpDto } from '../dtos/auth/local-sign-up.dto';
import { LocalSignInDto } from '../dtos/auth/local-sign-in.dto';
import { LocalSignInResponseDto } from '../dtos/auth/local-sign-in.response.dto';
import { AccessTokenPayloadDto } from '../dtos/auth/access-token-payload.dto';
import { TokenType } from '../dtos/auth/token-type.enum';
import { ethers } from 'ethers';
// OAuth2Client
import { LoginTicket, OAuth2Client, TokenPayload } from 'google-auth-library';
import { EthereumSigninChallengeRequestDto } from '../dtos/auth/ethereum-signin-challenge-request.dto';
import { EthereumSigninValidateRequestDto } from '../dtos/auth/ethereum-signin-validate-request.dto';
import { EthereumSigninChallengeResponseDto } from '../dtos/auth/ethereum-signin-challenge-response.dto';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import { SiweMessage } from 'siwe';
import { EmailDto } from './dtos/email.dto';
import { OkDto } from '../common/dtos/ok.dto';
import { UserDto } from '../dtos/user/user.dto';
import { classToPlain, instanceToPlain } from 'class-transformer';
import { CreateLocalUserDto } from '../dtos/user';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    private readonly configService: ApiConfigService,
    private readonly jwtService: JwtService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
  ) {}

  public async generateAccessToken(user: UserEntity): Promise<string> {
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      email: user.email,
      username: user.username,
      wallet: user.walletAddress,
      type: TokenType.AccessToken,
      // TODO: Add more claims.
    };

    // TODO: Make keys rotative.
    return this.jwtService.sign(payload, {
      algorithm: 'RS256',
      expiresIn: this.configService.authConfig.accessTokenExpiresIn,
      privateKey: this.configService.authConfig.accessTokenPrivateKey,
    });
  }

  public async generateRefreshToken(user: UserEntity): Promise<string> {
    if (!user) throw new UnauthorizedException('User not found');
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      email: user.email,
      username: user.username,
      wallet: user.walletAddress,
      type: TokenType.RefreshToken,
      // TODO: Add more claims.
    };

    // TODO: Make keys rotative.
    return this.jwtService.sign(payload, {
      algorithm: 'RS256',
      expiresIn: this.configService.authConfig.refreshTokenExpiresIn,
      privateKey: this.configService.authConfig.refreshTokenPrivateKey,
    });
  }

  public async refreshAccessToken(user: UserEntity) {
    return this.generateAccessToken(user);
  }

  public async signIn(user: UserEntity): Promise<LocalSignInResponseDto> {
    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return new LocalSignInResponseDto({
      user: user,
      accessToken: accessToken,
      refreshToken: refreshToken,
    });
  }

  public async signUpWithEmailUsernamePassword(
    data: LocalSignUpDto,
  ): Promise<LocalSignInResponseDto> {
    const passwordSalt = generateRandomSalt();
    const passwordHash = generateHash(data.password, passwordSalt);

    try {
      const user = await this.userService.createOneWithEmailAndPassword(
        new CreateLocalUserDto({
          // username: data.username,
          email: data.email,
          username: data.username,
          passwordHash: passwordHash,
          passwordSalt: passwordSalt,
        }),
      );

      // this.sendEmailVerification(user);

      return this.signIn(user);
    } catch (exception) {
      throw exception; // todo: fix this. useless.
    }
  }

  public async validateLocalSignIn(data: LocalSignInDto): Promise<UserEntity> {
    const { email, username, password } = data;

    const user = await this.userService.findOne({
      where: [{ email }, { username }],
    });

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

  public async userExists(user: string): Promise<boolean> {
    const foundUser = await this.userService.findOne({
      where: [{ email: user }, { username: user }],
      select: ['id'], // Only select the id to reduce payload size.
    });

    return Boolean(foundUser);
  }

  async signInWithEmailOrPassword(
    data: LocalSignInDto,
  ): Promise<LocalSignInResponseDto> {
    const user = await this.validateLocalSignIn(data);
    const response = await this.signIn(user);
    // const today = new Date();
    // const expiresOn = new Date(new Date().setDate(today.getDate() + 30));
    return {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      user: user,
    };
  }

  async sendMagicLink(data: EmailDto): Promise<OkDto> {
    let user = await this.userService.findOne({
      where: { email: data.email },
    });

    if (!user) user = await this.userService.createOneWithEmail(data.email);

    // send email with magic link
    const token = await this.generateRefreshToken(user);

    // send the email notification
    await this.notificationService.sendEmailNotification(
      data.email,
      'GameGuild Magic Link',
      `Use the following link to connect to Game Guild website: ${this.configService.hostFrontendUrl}/connect/?token=${token}`,
    );

    return { success: true, message: 'Email sent.' };
  }

  async generateWeb3SignInChallenge(
    data: EthereumSigninChallengeRequestDto,
  ): Promise<EthereumSigninChallengeResponseDto> {
    // use siwe to generate the message to be signed
    const termOfServiceUrl: string = `${this.configService.hostFrontendUrl}/tos`;
    const siwe = new SiweMessage({
      address: data.address,
      domain: new URL(this.configService.hostFrontendUrl).host,
      statement: `I accept the GameDev Guild Terms of Service: ${termOfServiceUrl}`,
      issuedAt: new Date().toISOString(),
      uri: this.configService.hostFrontendUrl,
      version: '1',
    });

    const key = `web3:challenge:message:${data.address}`;

    // TODO: It's a must because its what is accepted by the sign-in procedure.
    const message = siwe.prepareMessage(); //`0x${Buffer.from(challenge, 'utf8').toString('hex')}`;

    // stores it in the cache for 5 minutes
    await this.cacheManager.set(key, message, 5 * 60 * 1000);

    return { message };
  }

  async validateWeb3SignInChallenge(
    data: EthereumSigninValidateRequestDto,
  ): Promise<LocalSignInResponseDto> {
    const key = `web3:challenge:message:${data.address}`;

    const expectedMessage = await this.cacheManager.get<string>(key);

    if (!expectedMessage) {
      throw new UnauthorizedException(
        'Invalid message challenge. Maybe expired? Try again.',
      );
    }

    let walletAddress: string;
    try {
      walletAddress = ethers.verifyMessage(expectedMessage, data.signature);
    } catch (exception) {
      throw new UnauthorizedException('Invalid signature. Please try again.');
    }

    let user = await this.userService.findOne({
      where: { walletAddress: walletAddress },
      relations: { profile: true },
    });

    if (!user) {
      user = await this.userService.createOneWithWalletAddress(walletAddress);
    }

    // generate tokens
    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return {
      user,
      accessToken,
      refreshToken,
    };
  }

  async validateGoogleSignIn(idToken: string): Promise<LocalSignInResponseDto> {
    const client = new OAuth2Client(
      this.configService.authConfig.googleClientId,
    );
    let ticket: LoginTicket;
    try {
      ticket = await client.verifyIdToken({
        idToken: idToken,
      });
    } catch (exception) {
      throw new UnauthorizedException(
        'Unauthorized: ' + exception,
        'Invalid Google ID Token',
      );
    }
    const payload: TokenPayload = ticket.getPayload();

    let user = await this.userService.findOne({
      where: { googleId: payload.sub },
      relations: { profile: true },
    });
    if (!user) {
      user = await this.userService.createOneWithGoogleId(payload);
    }

    // generate tokens
    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return {
      user,
      accessToken,
      refreshToken,
    };
  }
}
