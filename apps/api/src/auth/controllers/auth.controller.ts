import { Body, Controller, Get, Logger, Param, Post, UseGuards } from '@nestjs/common';
import { CommandBus } from '@nestjs/cqrs';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { LocalGuard } from '@/auth/guards/local.guard';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { SignInWithGoogleGuard } from '@/auth/guards/sign-in-with-google.guard';
import { GenerateAuthTokensCommand } from '@/auth/commands/generate-auth-tokens.command';
import { User } from '@/auth/decorators/user.decorator';
import { UserDto } from '@/user/dtos/user.dto';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly commandBus: CommandBus) {}

  @Post('sign-in')
  @UseGuards(LocalGuard)
  @ApiBody({ type: LocalSignInRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  public async signInWithEmailAndPassword(@Body() data: Readonly<LocalSignInRequestDto>, @User() user: UserDto) {
    // return this.commandBus.execute(new LocalSignInCommand(data));
    return this.commandBus.execute(GenerateAuthTokensCommand.create(user));
  }

  @Get('google/callback/:idToken')
  @UseGuards(SignInWithGoogleGuard)
  @ApiResponse({ type: SignInResponseDto })
  async signInWithGoogle(@Param('idToken') idToken: string, @User() user: UserDto) {
    // return this.commandBus.execute(new SignInWithGoogleCommand(idToken));
    return this.commandBus.execute(GenerateAuthTokensCommand.create(user));
  }

  // @Public()
  // @Post('sign-up')
  // @ApiBody({ type: LocalSignUpRequestDto })
  // @ApiResponse({ type: SignInResponseDto })
  // public async signUpWithEmailAndPassword(@Body() data: Readonly<LocalSignUpRequestDto>) {
  //   // return this.commandBus.execute(new LocalSignUpCommand(data));
  // }

  // @Public()
  // @Post('refresh-token')
  // public async refreshToken() {
  //   // TODO: Implement this method.
  // }

  // @Public()
  // @Get('web3/challenge')
  // @HttpCode(HttpStatus.OK)
  // public async getWeb3Challenge() {
  //   // TODO: Implement this method.
  //   // It should return a challenge that the user can sign with their web3 wallet.
  //   // The challenge should be signed with the user's web3 wallet and sent back to the server.
  //   // The message should be a JWT token with the user's public address as the payload.
  // }

  // @Public()
  // @Post('web3/sign-in')
  // @HttpCode(HttpStatus.OK)
  // public async signInWithWeb3(@Body() data: Web3SignInDto) {
  //   return await this.authService.signInWithWeb3(data);
  // }

  // @Public()
  // @Post('web3/sign-up')
  // @HttpCode(HttpStatus.OK)
  // public async signUpWithWeb3(@Body() data: Web3SignUpDto) {
  //   return await this.authService.signUpWithWeb3(data);
  // }
}
