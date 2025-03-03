import { Injectable, Logger } from '@nestjs/common';
import { CommandBus } from '@nestjs/cqrs';
import { PassportStrategy } from '@nestjs/passport';
import { Strategy } from 'passport-local';
import { ValidateLocalSignInCommand } from '@/auth/commands/validate-local-sign-in.command';
import { LocalSignInRequestDto } from '@/auth/dtos/local-sign-in-request.dto';
import { UserDto } from '@/user/dtos/user.dto';

@Injectable()
export class LocalSignInStrategy extends PassportStrategy(Strategy) {
  private readonly logger = new Logger(LocalSignInStrategy.name);

  constructor(private readonly commandBus: CommandBus) {
    super({});
  }

  public async validate(data: LocalSignInRequestDto): Promise<UserDto> {
    try {
      return await this.commandBus.execute(ValidateLocalSignInCommand.create(data));
    } catch (error) {
      throw error;
    }
  }
}
