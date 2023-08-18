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
import { IsPublic } from './decorators/isPublic.decorator';

@Controller()
export class AuthController {
  constructor(private authService: AuthService) {}

  @IsPublic()
  @Post('sign-in')
  @UseGuards(AuthGuard('local'))
  async signInWithEmailAndPassword(@Request() req) {
    return this.authService.signJwt(req.user);
  }

  @IsPublic()
  @Post('sign-up')
  @HttpCode(HttpStatus.CREATED)
  async signUpWithEmailAndPassword(@Body() data: UserEmailAndPassword) {
    return this.authService.signUpWithEmailAndPassword({
      email: data.email,
      password: data.password,
    });
  }

  @Get('profile')
  async getProfile(@Request() req) {
    const id = Number(req.user.userId);
    return await this.authService.getProfileOfCurrentUser(id);
  }
}
