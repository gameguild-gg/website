import {
  Body,
  Controller,
  Get,
  Logger,
  Post,
  Query,
  Request,
  UseGuards,
} from '@nestjs/common';
import { ApiBody, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { Public } from './decorators';
import { LocalGuard } from './guards';
import { JwtRefreshTokenGuard } from './guards/jwt-refresh-token-guard.service';
import { RequestWithUser } from './types';
import { LocalSignInDto } from "../dtos/auth/local-sign-in.dto";
import { LocalSignUpDto } from "../dtos/auth/local-sign-up.dto";

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

  @Post('/refresh-token')
  @Public()
  @UseGuards(JwtRefreshTokenGuard)
  public async refreshToken(@Request() request: RequestWithUser) {
    return await this.authService.refreshAccessToken(request.user);
  }

  @Get('verify-email')
  public async verifyEmail(@Query('token') token: string): Promise<any> {
    return await this.authService.validateEmailVerificationToken(token);
  }
}
