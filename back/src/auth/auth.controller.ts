import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Post,
  Request,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { AuthService } from './auth.service';
import { UserEmailAndPassword } from './interfaces/UserEmailAndPassword.interface';

@Controller('auth')
export class AuthController {
  constructor(private authService: AuthService) {}

  @Post('sign-in')
  @UseGuards(AuthGuard('local'))
  async signInWithEmailAndPassword(@Request() req) {
    return this.authService.signJwt(req.user);
  }

  @Post('sign-up')
  @HttpCode(HttpStatus.CREATED)
  async signUpWithEmailAndPassword(@Body() data: UserEmailAndPassword) {
    return this.authService.signUpWithEmailAndPassword({
      email: data.email,
      password: data.password,
    });
  }

  @UseGuards(AuthGuard('jwt'))
  @Get('profile')
  async getProfile(@Request() req) {
    return req.user;
  }
}
