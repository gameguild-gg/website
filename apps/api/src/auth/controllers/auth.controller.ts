import { Body, Controller, Get, Logger, Post, UseGuards } from '@nestjs/common';
import { CommandBus } from '@nestjs/cqrs';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { LocalSignInGuard } from '@/auth/guards/local-sign-in.guard';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { GoogleSignInGuard } from '@/auth/guards/google-sign-in.guard';
import { GenerateSignInResponseCommand } from '@/auth/commands/generate-sign-in-response.command';
import { User } from '@/auth/decorators/user.decorator';
import { UserDto } from '@/user/dtos/user.dto';
import { Public } from '@/auth/decorators/public.decorator';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly commandBus: CommandBus) {}

  @Post('sign-in')
  @UseGuards(LocalSignInGuard)
  @ApiBody({ type: LocalSignInRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  public async localSignIn(@Body() data: Readonly<LocalSignInRequestDto>, @User() user: UserDto): Promise<SignInResponseDto> {
    return this.commandBus.execute(GenerateSignInResponseCommand.create(user));
  }

  @Public()
  @Get('google/sign-in')
  @UseGuards(GoogleSignInGuard)
  public async googleSignIn() {}

  @Public()
  @Get('google/callback')
  @UseGuards(GoogleSignInGuard)
  @ApiResponse({ type: SignInResponseDto })
  public async googleSignInCallback(@User() user: UserDto): Promise<SignInResponseDto> {
    return this.commandBus.execute(GenerateSignInResponseCommand.create(user));
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
