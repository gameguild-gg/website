import { Body, Controller, Get, HttpCode, HttpStatus, Logger, Post, UseGuards } from '@nestjs/common';
import { CommandBus } from '@nestjs/cqrs';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { LocalSignInGuard } from '@/auth/guards/local-sign-in.guard';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { LocalSignUpRequestDto } from '@/auth/dtos/local-sign-up-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { GoogleSignInGuard } from '@/auth/guards/google-sign-in.guard';
import { GenerateSignInResponseCommand } from '@/auth/commands/generate-sign-in-response.command';
import { LocalSignUpCommand } from '@/auth/commands/local-sign-up.command';
import { Public } from '@/auth/decorators/public.decorator';
import { User } from '@/auth/decorators/user.decorator';
import { UserDto } from '@/user/dtos/user.dto';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly commandBus: CommandBus) {}

  @Post('sign-in')
  @UseGuards(LocalSignInGuard)
  @ApiBody({ type: LocalSignInRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  @HttpCode(HttpStatus.OK)
  public async localSignIn(@Body() data: Readonly<LocalSignInRequestDto>, @User() user: UserDto): Promise<SignInResponseDto> {
    return this.commandBus.execute(GenerateSignInResponseCommand.create(user));
  }

  @Public()
  @Post('sign-up')
  @ApiBody({ type: LocalSignUpRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  public async signUpWithEmailAndPassword(@Body() data: Readonly<LocalSignUpRequestDto>) {
    return this.commandBus.execute(LocalSignUpCommand.create(data));
  }

  @Public()
  @Get('google/sign-in')
  @UseGuards(GoogleSignInGuard)
  @HttpCode(HttpStatus.OK)
  public async googleSignIn() {}

  @Public()
  @Get('google/callback')
  @UseGuards(GoogleSignInGuard)
  @ApiResponse({ type: SignInResponseDto })
  @HttpCode(HttpStatus.OK)
  public async googleSignInCallback(@User() user: UserDto): Promise<SignInResponseDto> {
    return this.commandBus.execute(GenerateSignInResponseCommand.create(user));
  }

  @Public()
  @Get('web3/sign-in')
  @HttpCode(HttpStatus.OK)
  public async generateWeb3SignInChallenge() {
    // TODO: Implement this method.
    // It should return a challenge that the user can sign with their web3 wallet.
    // The challenge should be signed with the user's web3 wallet and sent back to the server.
    // The message should be a JWT token with the user's public address as the payload.
    // return this.commandBus.execute(GenerateWeb3SignInChallengeCommand.create(null));
  }

  @Public()
  @Post('web3/sign-in')
  @HttpCode(HttpStatus.OK)
  public async web3SignIn(@User() user: UserDto) {
    return this.commandBus.execute(GenerateSignInResponseCommand.create(user));
  }

  @Public()
  @Post('token')
  public async refreshToken() {
    // TODO: Implement this method.
  }
}
