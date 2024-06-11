import { Injectable, Logger, UnauthorizedException } from '@nestjs/common';
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

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    private readonly configService: ApiConfigService,
    private readonly jwtService: JwtService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
  ) {}

  public async generateAccessToken(user: UserEntity): Promise<string> {
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      email: user.email,
      username: user.username,
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
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      email: user.email,
      username: user.username,
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

  public async generateEmailVerificationToken(
    user: UserEntity,
  ): Promise<string> {
    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      email: user.email,
      username: user.username,
      type: TokenType.EmailConfirmationToken,
      // TODO: Add more claims.
    };

    return this.jwtService.sign(payload, {
      algorithm: 'RS256',
      expiresIn: this.configService.authConfig.emailVerificationTokenExpiresIn,
      privateKey:
        this.configService.authConfig.emailVerificationTokenPrivateKey,
    });
  }

  public async refreshAccessToken(user: UserEntity) {
    return this.generateAccessToken(user);
  }

  public async signIn(user: UserEntity) {
    const accessToken = await this.generateAccessToken(user);
    const refreshToken = await this.generateRefreshToken(user);

    return {
      user: user,
      accessToken: accessToken,
      refreshToken: refreshToken,
    };
  }

  public async signUpWithEmailUsernamePassword(
    data: LocalSignUpDto,
  ): Promise<LocalSignInResponseDto> {
    const passwordSalt = generateRandomSalt();
    const passwordHash = generateHash(data.password, passwordSalt);

    try {
      const user = await this.userService.createOneWithEmailAndPassword({
        // username: data.username,
        email: data.email,
        username: data.username,
        passwordHash: passwordHash,
        passwordSalt: passwordSalt,
      });

      // this.sendEmailVerification(user);

      return await this.signIn(user);
    } catch (exception) {
      throw exception; // todo: fix this. useless.
    }
  }

  public async sendEmailVerification(email: string) {
    const user = await this.userService.findOne({ where: { email } });
    const token = await this.generateEmailVerificationToken(user);
    const url = `${this.configService.authConfig.emailVerificationUrl}?token=${token}`;
    const subject = 'One Time Password - GameGuild';
    const message = `Please verify your email by clicking on the link: ${url}`;

    await this.notificationService.sendEmailNotification(
      user.email,
      subject,
      message,
    );
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

  public async validateEmailVerificationToken(token: string) {
    // TODO: Validate email verification token.
    try {
      const decodedToken = this.jwtService.verify(token, {
        publicKey:
          this.configService.authConfig.emailVerificationTokenPublicKey,
      });

      const user = await this.userService.findOne({
        where: { id: decodedToken.sub },
      });

      user.emailVerified = true;
      await this.userService.save(user);
    } catch (exception) {
      throw new UnauthorizedException('Invalid or expired token');
    }
  }

  public async userExists(user: string): Promise<boolean> {
    const foundUser = await this.userService.findOne({
      where: [{ email: user }, { username: user }],
      select: ['id'], // Only select the id to reduce payload size.
    });

    return !!foundUser;
  }

  async signInWithEmailOrPassword(
    data: LocalSignInDto,
  ): Promise<LocalSignInResponseDto> {
    const user = await this.validateLocalSignIn(data);
    const response = await this.signIn(user);
    const today = new Date();
    const expiresOn = new Date(new Date().setDate(today.getDate() + 30));
    return {
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      user: user,
    };
  }

  async sendMagicLink(email: string) {
    let user = await this.userService.findOne({
      where: { email },
    });

    if (!user) user = await this.userService.findOneBy({ email });
  }
}
