import { Body, Controller, Get, Logger, Param, Post } from '@nestjs/common';
import { ApiBody, ApiProperty, ApiResponse, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { AuthUser } from './decorators';
import { LocalSignInDto } from '../dtos/auth/local-sign-in.dto';
import { LocalSignUpDto } from '../dtos/auth/local-sign-up.dto';
import { LocalSignInResponseDto } from '../dtos/auth/local-sign-in.response.dto';
import { UserEntity } from '../user/entities';
import { Auth } from './decorators/http.decorator';
import { EthereumSigninValidateRequestDto } from '../dtos/auth/ethereum-signin-validate-request.dto';
import { EthereumSigninChallengeRequestDto } from '../dtos/auth/ethereum-signin-challenge-request.dto';
import { EthereumSigninChallengeResponseDto } from '../dtos/auth/ethereum-signin-challenge-response.dto';
import { EmailDto } from './dtos/email.dto';
import { OkDto } from '../common/dtos/ok.dto';
import { OkResponse } from '../common/decorators/return-type.decorator';
import { AuthType } from './guards';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly authService: AuthService) {}

  @Post('magic-link')
  //@Public()
  @OkResponse({ type: OkDto })
  public async magicLink(@Body() body: EmailDto): Promise<OkDto> {
    return this.authService.sendMagicLink(body);
  }

  // @Post('sign-in')
  // //@Public()
  // @UseGuards(LocalGuard)
  // @ApiBody({ type: LocalSignInDto })
  // public async signInWithEmailAndPassword(@Request() request: RequestWithUser) {
  //   // TODO: Implement this endpoint.
  //   return await this.authService.signIn(request.user);
  // }

  @Post('local/sign-in')
  //@Public(true)
  @ApiBody({ type: LocalSignInDto })
  public async localSignWithEmailOrUsername(
    @Body() data: LocalSignInDto,
  ): Promise<LocalSignInResponseDto> {
    return this.authService.signInWithEmailOrPassword(data);
  }

  @Post('local/sign-up')
  //@Public()
  @OkResponse({ type: LocalSignInResponseDto }) // pass the type to the swagger
  public async signUpWithEmailUsernamePassword(
    @Body() data: LocalSignUpDto,
  ): Promise<LocalSignInResponseDto> {
    const resp = await this.authService.signUpWithEmailUsernamePassword(data);
    return resp;
  }

  // TODO: Implement this endpoint.
  // @Post('github/callback')
  // //@Public()
  // public async signInWithGithub(@Request() request: RequestWithUser) {
  //   return await this.authService.signIn(request.user);
  // }

  @Get('google/callback/:token')
  @OkResponse({ type: LocalSignInResponseDto })
  //@Public()
  public async signInWithGoogle(
    @Param('token') token: string,
  ): Promise<LocalSignInResponseDto> {
    return this.authService.validateGoogleSignIn(token);
  }

  @Post('web3/sign-in/challenge')
  //@Public()
  @OkResponse({ type: EthereumSigninChallengeResponseDto })
  public async getWeb3SignInChallenge(
    @Body() data: EthereumSigninChallengeRequestDto,
  ): Promise<EthereumSigninChallengeResponseDto> {
    return this.authService.generateWeb3SignInChallenge(data);
  }

  @Post('web3/sign-in/validate')
  //@Public()
  @OkResponse({ type: LocalSignInResponseDto })
  public async validateWeb3SignInChallenge(
    @Body() data: EthereumSigninValidateRequestDto,
  ): Promise<LocalSignInResponseDto> {
    return this.authService.validateWeb3SignInChallenge(data);
  }

  @Get('me')
  @Auth({ guard: AuthType.AccessToken })
  @OkResponse({ type: UserEntity })
  public async getCurrentUser(
    @AuthUser() user: UserEntity,
  ): Promise<UserEntity> {
    return user;
  }

  @Get('refresh-token')
  @Auth({ guard: AuthType.RefreshToken })
  @OkResponse({ type: LocalSignInResponseDto })
  public async refreshToken(
    @AuthUser() user: UserEntity,
  ): Promise<LocalSignInResponseDto> {
    return this.authService.refreshAccessToken(user);
  }

  // @Get('verify-email')
  // public async verifyEmail(@Query('token') token: string): Promise<any> {
  //   return await this.authService.validateEmailVerificationToken(token);
  // }

  @Get('userExists/:user')
  //@Public()
  public async userExists(@Param('user') user: string): Promise<boolean> {
    return this.authService.userExists(user);
  }
}
