import { Injectable, Logger, UnauthorizedException } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { ApiConfigService } from "../common/config.service";
import { generateHash, generateRandomSalt, validateHash, } from '../common/utils/hash';
import { NotificationService } from "../notification/notification.service";
import { UserEntity } from '../user/entities';
import { UserService } from '../user/user.service';
import { LocalSignInDto, LocalSignUpDto } from './dtos';

@Injectable()
export class AuthService {
  private readonly logger = new Logger(AuthService.name);

  constructor(
    private readonly configService: ApiConfigService,
    private readonly jwtService: JwtService,
    private readonly userService: UserService,
    private readonly notificationService: NotificationService,
  ) { }

  public async generateAccessToken(user: UserEntity): Promise<any> {
    const payload = {
      sub: user.id,
      email: user.email,
      // TODO: Add more claims.
    };
    
    // TODO: Make keys rotative.
    return this.jwtService.sign(
      payload,
      {
        algorithm: 'RS256',
        expiresIn: this.configService.authConfig.accessTokenExpiresIn,
        privateKey: this.configService.authConfig.accessTokenPrivateKey,
      }
    );
  }

  public async generateRefreshToken(user: UserEntity): Promise<any> {
    const payload = {
      sub: user.id,
      email: user.email,
      // TODO: Add more claims.
    };
    
    // TODO: Make keys rotative.
    return this.jwtService.sign(
      payload,
      {
        algorithm: 'RS256',
        expiresIn: this.configService.authConfig.refreshTokenExpiresIn,
        privateKey: this.configService.authConfig.refreshTokenPrivateKey,
      }
    );
  }

  public async generateEmailVerificationToken(user: UserEntity): Promise<any> {
    const payload = {
      sub: user.id,
      email: user.email,
      // TODO: Add more claims.
    };

    return this.jwtService.sign(
      payload,
      {
        algorithm: 'RS256',
        expiresIn: this.configService.authConfig.emailVerificationTokenExpiresIn,
        privateKey: this.configService.authConfig.emailVerificationTokenPrivateKey,
      }
    );
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

  public async signUpWithEmailAndPassword(data: LocalSignUpDto) {
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

      this.sendEmailVerification(user);

      return user;
    } catch (exception) {
      throw exception;
    }
  }

  public async sendEmailVerification(user: UserEntity) {
    const token = await this.generateEmailVerificationToken(user);
    const url = `${ this.configService.authConfig.emailVerificationUrl }?token=${ token }`;
    const subject = 'Verify your email';
    const message = `Please verify your email by clicking on the link: ${ url }`;

    await this.notificationService.sendEmailNotification(user.email, subject, message);
  }

  public async validateLocalSignIn(data: LocalSignInDto): Promise<UserEntity> {
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

  public async validateEmailVerificationToken(token: string) {
    // TODO: Validate email verification token.
    try {
      const decodedToken = this.jwtService.verify(
        token,
        {
          publicKey: this.configService.authConfig.emailVerificationTokenPublicKey,
        }
      );

      const user = await this.userService.findOne({ where: { id: decodedToken.sub } });

      user.emailVerified = true;
      await this.userService.save(user);

    } catch (exception) {
      throw new UnauthorizedException('Invalid or expired token');
    }
  }
}
