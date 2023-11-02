import { Body, Controller, Logger, Post, Request, UseGuards, } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { Public } from './decorators';
import { LocalSignUpDto } from './dtos';
import { LocalGuard } from './guards';
import { RequestWithUser } from './types';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  private readonly logger = new Logger(AuthController.name);

  constructor(private readonly authService: AuthService) {}

  @Post('sign-in')
  @Public()
  @UseGuards(LocalGuard)
  // @ApiBody({ type: LocalSignInDto })
  public async signInWithEmailAndPassword(@Request() request: RequestWithUser) {
    // TODO: Implement this endpoint.
    return await this.authService.signIn(request.user);
  }

  @Post('github/callback')
  @Public()
  public async signInWithGithub(@Request() request: RequestWithUser) {
    // TODO: Implement this endpoint.
    return await this.authService.signIn(request.user);
  }

  @Post('google/callback')
  @Public()
  public async signInWithGoogle(@Request() request: RequestWithUser) {
    // TODO: Implement this endpoint.
    return await this.authService.signIn(request.user);
  }

  @Post('sign-up')
  @Public()
  public async signUpWithEmailAndPassword(@Body() data: LocalSignUpDto) {
    // TODO: Implement this endpoint.
    return this.authService.signUpWithEmailAndPassword(data);
  }

  @Post('sign-out')
  public async signOut(@Request() request: RequestWithUser) {
    // TODO: Implement this endpoint.
  }

  @Post('/refresh-token')
  public async refreshToken(@Request() request) {
    // TODO: Implement this endpoint.
  }
}
