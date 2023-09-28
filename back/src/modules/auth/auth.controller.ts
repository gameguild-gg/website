import {
  Body,
  Controller,
  NotImplementedException,
  Post,
} from '@nestjs/common';
import { AuthService } from './auth.service';
import { ApiTags } from '@nestjs/swagger';
import { UserLoginDto } from './user-login.dto';
import { TokenPayloadDto } from './token-payload.dto';
import { UserEntity } from '../user/user.entity';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  constructor(public service: AuthService) {}

  @Post('register/email-password')
  public async registerEmailPassword(
    @Body() data: UserLoginDto,
  ): Promise<UserEntity> {
    return await this.service.registerEmailPass(data);
  }

  @Post('validate-email')
  public async validadeEmail(token: string): Promise<boolean> {
    return await this.service.validadeEmailToken(token);
  }

  @Post('login/email-password')
  public async loginEmailPassword(
    @Body() data: UserLoginDto,
  ): Promise<TokenPayloadDto> {
    return await this.service.loginUserEmailPass(data);
  }
}
