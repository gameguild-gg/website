import {
  Body,
  Controller,
  NotImplementedException,
  Post,
} from '@nestjs/common';
import { AuthService } from './auth.service';
import { ApiTags } from '@nestjs/swagger';
import { UserLoginDto } from './dtos/user-login.dto';
import { TokenPayloadDto } from './dtos/token-payload.dto';
import { UserEntity } from '../user/user.entity';
import { SimpleUserRegisterDto } from './dtos/simple-user-register.dto';

@Controller('auth')
@ApiTags('auth')
export class AuthController {
  constructor(public service: AuthService) {}

  @Post('register/simple-register')
  public async simpleRegister(
    @Body() data: SimpleUserRegisterDto,
  ): Promise<UserEntity> {
    return await this.service.simpleRegister(data);
  }
  @Post('register/email-password')
  public async registerEmailPassword(
    @Body() data: UserLoginDto,
  ): Promise<UserEntity> {
    return await this.service.registerEmailPass(data);
  }

  @Post('validate-email')
  public async validadeEmail(token: string): Promise<UserEntity> {
    return await this.service.validadeEmailToken(token);
  }

  @Post('login/email-password')
  public async loginEmailPassword(
    @Body() data: UserLoginDto,
  ): Promise<TokenPayloadDto> {
    return await this.service.loginUserEmailPass(data);
  }
}
