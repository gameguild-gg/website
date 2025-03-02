import { Body, Controller, Get, Logger, Param, Post, UseGuards } from '@nestjs/common';
import { CommandBus } from '@nestjs/cqrs';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { Public } from '@/auth/decorators/public.decorator';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { LocalSignUpRequestDto } from '@/auth/dtos/local-sign-up-request.dto';
import { SignInResponseDto } from '@/auth/dtos/sign-in-response.dto';
import { LocalGuard } from '@/auth/guards/local.guard';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(
    private readonly commandBus: CommandBus,
    // private readonly queryBus: QueryBus,
  ) {}

  // @Public()
  @Post('sign-in')
  @UseGuards(LocalGuard)
  @ApiBody({ type: LocalSignInRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  public async signInWithEmailAndPassword(@Body() data: Readonly<LocalSignInRequestDto>) {
    // return this.commandBus.execute(new LocalSignInCommand(data));
  }

  @Public()
  @Get('google/callback/:idToken')
  // @UseGuards(SignInWithGoogleGuard)
  @ApiResponse({ type: SignInResponseDto })
  async signInWithGoogle(@Param('idToken') idToken: string) {
    // return this.commandBus.execute(new SignInWithGoogleCommand(idToken));
  }

  @Public()
  @Post('sign-up')
  @ApiBody({ type: LocalSignUpRequestDto })
  @ApiResponse({ type: SignInResponseDto })
  public async signUpWithEmailAndPassword(@Body() data: Readonly<LocalSignUpRequestDto>) {
    // return this.commandBus.execute(new LocalSignUpCommand(data));
  }

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
