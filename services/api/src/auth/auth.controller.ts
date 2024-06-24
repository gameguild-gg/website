import {
  Body,
  Controller,
  Get,
  Logger,
  Param,
  Post,
  Query,
} from '@nestjs/common';
import { ApiOkResponse, ApiResponse, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { AuthUser, Public } from './decorators';
import { LocalSignInDto } from '../dtos/auth/local-sign-in.dto';
import { LocalSignUpDto } from '../dtos/auth/local-sign-up.dto';
import { LocalSignInResponseDto } from '../dtos/auth/local-sign-in.response.dto';
import { UserDto } from '../dtos/user/user.dto';
import { UserEntity } from '../user/entities';
import { Auth } from './decorators/http.decorator';
import { EthereumSigninValidateRequestDto } from '../dtos/auth/ethereum-signin-validate-request.dto';
import { EthereumSigninChallengeRequestDto } from '../dtos/auth/ethereum-signin-challenge-request.dto';
import { EthereumSigninChallengeResponseDto } from '../dtos/auth/ethereum-signin-challenge-response.dto';
import { EmailDto } from './dtos/email.dto';
import { OkDto } from '../common/dtos/ok.dto';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly authService: AuthService) {}

  @Post('magic-link')
  @Public()
  @ApiOkResponse({ type: OkDto })
  public async magicLink(@Body() body: EmailDto): Promise<OkDto> {
    return await this.authService.sendMagicLink(body);
  }

  // @Post('sign-in')
  // @Public()
  // @UseGuards(LocalGuard)
  // @ApiBody({ type: LocalSignInDto })
  // public async signInWithEmailAndPassword(@Request() request: RequestWithUser) {
  //   // TODO: Implement this endpoint.
  //   return await this.authService.signIn(request.user);
  // }

  @Post('local/sign-in')
  @Public(true)
  public async localSignWithEmailOrUsername(
    @Body() data: LocalSignInDto,
  ): Promise<LocalSignInResponseDto> {
    return await this.authService.signInWithEmailOrPassword(data);
  }

  @Post('local/sign-up')
  @Public()
  @ApiResponse({ type: LocalSignInResponseDto })
  public async signUpWithEmailUsernamePassword(
    @Body() data: LocalSignUpDto,
  ): Promise<LocalSignInResponseDto> {
    return this.authService.signUpWithEmailUsernamePassword(data);
  }

  // TODO: Implement this endpoint.
  // @Post('github/callback')
  // @Public()
  // public async signInWithGithub(@Request() request: RequestWithUser) {
  //   return await this.authService.signIn(request.user);
  // }

  @Get('google/callback/:token')
  @ApiOkResponse({ type: LocalSignInResponseDto })
  @Public()
  public async signInWithGoogle(
    @Param('token') token: string,
  ): Promise<LocalSignInResponseDto> {
    return await this.authService.validateGoogleSignIn(token);
  }

  @Post('web3/sign-in/challenge')
  @Public()
  @ApiOkResponse({ type: EthereumSigninChallengeResponseDto })
  public async getWeb3SignInChallenge(
    @Body() data: EthereumSigninChallengeRequestDto,
  ): Promise<EthereumSigninChallengeResponseDto> {
    return await this.authService.generateWeb3SignInChallenge(data);
  }

  @Post('web3/sign-in/validate')
  @Public()
  @ApiOkResponse({ type: LocalSignInResponseDto })
  public async validateWeb3SignInChallenge(
    @Body() data: EthereumSigninValidateRequestDto,
  ): Promise<LocalSignInResponseDto> {
    return await this.authService.validateWeb3SignInChallenge(data);
  }

  @Get('current-user')
  @Auth()
  @ApiOkResponse({ type: UserDto })
  public async getCurrentUser(@AuthUser() user: UserEntity): Promise<UserDto> {
    return user;
  }

  // @Post('refresh-token')
  // @Public()
  // @UseGuards(JwtRefreshTokenGuard)
  // public async refreshToken(@Request() request: RequestWithUser) {
  //   return await this.authService.refreshAccessToken(request.user);
  // }

  @Get('verify-email')
  public async verifyEmail(@Query('token') token: string): Promise<any> {
    return await this.authService.validateEmailVerificationToken(token);
  }

  @Get('userExists/:user')
  @Public()
  public async userExists(@Param('user') user: string): Promise<boolean> {
    return await this.authService.userExists(user);
  }
}
