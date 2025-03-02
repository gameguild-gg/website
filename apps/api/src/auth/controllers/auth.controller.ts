import { Body, Controller, Logger, Post } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { Public } from '@/auth/decorators/public.decorator';
import { LocalSignInRequest } from '@/auth/dtos/local-sign-in-request.dto';
import { LocalSignUpRequest } from '@/auth/dtos/local-sign-up-request.dto';
import { CommandBus, QueryBus } from '@nestjs/cqrs';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(
    private readonly commandBus: CommandBus,
    private readonly queryBus: QueryBus,
  ) {}

  @Public()
  @Post('sign-in')
  // @ApiBody({ type: LocalSignInRequest })
  public async signInWithEmailAndPassword(@Body() data: Readonly<LocalSignInRequest>) {
    // return await this.authService.signInWithEmailAndPassword(data);
  }

  @Public()
  @Post('sign-up')
  // @ApiBody({ type: LocalSignUpRequest })
  public async signUpWithEmailAndPassword(@Body() data: Readonly<LocalSignUpRequest>) {
    // return await this.authService.signUpWithEmailAndPassword(data);
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
