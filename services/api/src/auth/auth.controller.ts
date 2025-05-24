import { Body, Controller, Get, Logger, Param, Post } from '@nestjs/common';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { AuthUser } from './decorators';
import { LocalSignUpDto, LocalSignInDto } from '../dtos/auth';
import { LocalSignInResponseDto } from '../dtos/auth/local-sign-in.response.dto';
import { UserEntity } from '../user/entities';
import { Auth } from './decorators';
import { EthereumSigninValidateRequestDto } from '../dtos/auth/ethereum-signin-validate-request.dto';
import { EthereumSigninChallengeRequestDto } from '../dtos/auth/ethereum-signin-challenge-request.dto';
import { EthereumSigninChallengeResponseDto } from '../dtos/auth/ethereum-signin-challenge-response.dto';
import { EmailDto } from './dtos/email.dto';
import { OkDto } from '../common/dtos/ok.dto';
import { AuthenticatedRoute, PublicRoute, RefreshTokenRoute } from './auth.enum';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly authService: AuthService) {}

  // todo: make a way to validade the email automatically if the user logs in with this method

  @Post('magic-link')
  @Auth(PublicRoute)
  @ApiResponse({ type: OkDto })
  public async magicLink(@Body() body: EmailDto): Promise<OkDto> {
    return this.authService.sendMagicLink(body);
  }

  @Post('local/sign-up')
  @Auth(PublicRoute)
  @ApiResponse({ type: LocalSignInResponseDto }) // pass the type to the swagger
  public async signUpWithEmailUsernamePassword(@Body() data: LocalSignUpDto): Promise<LocalSignInResponseDto> {
    const resp = await this.authService.signUpWithEmailUsernamePassword(data);
    return resp;
  }

  @Get('google/callback/:token')
  @ApiResponse({ type: LocalSignInResponseDto })
  @Auth(PublicRoute)
  public async signInWithGoogle(@Param('token') token: string): Promise<LocalSignInResponseDto> {
    return this.authService.validateGoogleSignIn(token);
  }

  @Post('web3/sign-in/challenge')
  @Auth(PublicRoute)
  @ApiResponse({ type: EthereumSigninChallengeResponseDto })
  public async getWeb3SignInChallenge(@Body() data: EthereumSigninChallengeRequestDto): Promise<EthereumSigninChallengeResponseDto> {
    return this.authService.generateWeb3SignInChallenge(data);
  }

  @Post('web3/sign-in/validate')
  @Auth(PublicRoute)
  @ApiResponse({ type: LocalSignInResponseDto })
  public async validateWeb3SignInChallenge(@Body() data: EthereumSigninValidateRequestDto): Promise<LocalSignInResponseDto> {
    return this.authService.validateWeb3SignInChallenge(data);
  }

  @Get('me')
  @Auth(AuthenticatedRoute)
  @ApiResponse({ type: UserEntity })
  public async getCurrentUser(@AuthUser() user: UserEntity): Promise<UserEntity> {
    return this.authService.getUserWithProfile(user.id);
  }

  @Get('refresh-token')
  @Auth(RefreshTokenRoute)
  @ApiResponse({ type: LocalSignInResponseDto })
  public async refreshToken(@AuthUser() user: UserEntity): Promise<LocalSignInResponseDto> {
    return this.authService.refreshAccessToken(user);
  }

  @Get('userExists/:user')
  @Auth(PublicRoute)
  @ApiResponse({ type: Boolean })
  public async userExists(@Param('user') user: string): Promise<boolean> {
    return this.authService.userExists(user);
  }
}
