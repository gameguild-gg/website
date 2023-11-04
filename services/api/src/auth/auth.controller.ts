import { Body, Controller, Logger, Post, Request, UseGuards, } from '@nestjs/common';
import { ApiBody, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { Public } from './decorators';
import { LocalSignInDto, LocalSignUpDto } from './dtos';
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
  @ApiBody({ type: LocalSignInDto })
  public async signInWithEmailAndPassword(@Request() request: RequestWithUser) {
    // TODO: Implement this endpoint.
    return await this.authService.signIn(request.user);
  }

  // TODO: Implement this endpoint.
  // @Post('github/callback')
  // @Public()
  // public async signInWithGithub(@Request() request: RequestWithUser) {
  //   return await this.authService.signIn(request.user);
  // }

  // TODO: Implement this endpoint.
  // @Post('google/callback')
  // @Public()
  // public async signInWithGoogle(@Request() request: RequestWithUser) {
  //   return await this.authService.signIn(request.user);
  // }

  @Post('sign-up')
  @Public()
  public async signUpWithEmailAndPassword(@Body() data: LocalSignUpDto) {
    return this.authService.signUpWithEmailAndPassword(data);
  }

  // TODO: Implement this endpoint.
  // @Post('/refresh-token')
  // public async refreshToken(@Request() request) {
  //  
  // }
}
