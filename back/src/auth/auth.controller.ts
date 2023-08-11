import {
  Controller,
  Request,
  Post,
  UseGuards,
  Get,
  Body,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { AuthService } from './auth.service';

@Controller('auth')
export class AuthController {
  constructor(private authService: AuthService) {}

  @UseGuards(AuthGuard('local'))
  @Post('sign-in')
  async signInWithEmailAndPassword(@Request() req) {
    return this.authService.signInWithEmailAndPassword(req.user);
  }

  @Post('sign-up')
  async signUpWithEmailAndPassword(@Body() data: Record<string, string>) {
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
