import { Inject, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { JwtService } from '@nestjs/jwt';

import { GenerateEmailVerificationTokenCommand } from '@/auth/commands/generate-email-verification-token.command';
import { emailVerificationTokenConfig } from '@/auth/config/email-verification-token.config';
import { EmailVerificationTokenPayloadDto } from '@/auth/dtos/email-verification-token-payload.dto';
import { TokenType } from '@/auth/dtos/token-type.enum';

@CommandHandler(GenerateEmailVerificationTokenCommand)
export class GenerateEmailVerificationTokenHandler implements ICommandHandler<GenerateEmailVerificationTokenCommand> {
  private readonly logger = new Logger(GenerateEmailVerificationTokenHandler.name);

  constructor(
    @Inject(emailVerificationTokenConfig.KEY)
    private readonly config: ConfigType<typeof emailVerificationTokenConfig>,
    private readonly jwtService: JwtService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: GenerateEmailVerificationTokenCommand): Promise<string> {
    const { user } = command;

    const payload: EmailVerificationTokenPayloadDto = {
      sub: user.id,
      type: TokenType.EmailVerificationToken,
    };

    // TODO: Make keys rotate.
    return this.jwtService.sign(payload, {
      algorithm: this.config.signOptions.algorithm,
      expiresIn: this.config.signOptions.expiresIn,
      privateKey: this.config.privateKey,
    });
  }
}
