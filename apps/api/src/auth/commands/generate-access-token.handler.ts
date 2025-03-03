import { Inject, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { JwtService } from '@nestjs/jwt';

import { GenerateAccessTokenCommand } from '@/auth/commands/generate-access-token.command';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { AccessTokenPayloadDto } from '@/auth/dtos/access-token-payload.dto';
import { TokenType } from '@/auth/dtos/token-type.enum';

@CommandHandler(GenerateAccessTokenCommand)
export class GenerateAccessTokenHandler implements ICommandHandler<GenerateAccessTokenCommand> {
  private readonly logger = new Logger(GenerateAccessTokenHandler.name);

  constructor(
    @Inject(accessTokenConfig.KEY)
    private readonly config: ConfigType<typeof accessTokenConfig>,
    private readonly jwtService: JwtService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: GenerateAccessTokenCommand): Promise<string> {
    const { user } = command;

    const payload: AccessTokenPayloadDto = {
      sub: user.id,
      // email: user.email,
      // username: user.username,
      // wallet: user.walletAddress,
      type: TokenType.AccessToken,
      // TODO: Add more claims.
    };

    // TODO: Make keys rotate.
    return this.jwtService.sign(payload, {
      algorithm: this.config.signOptions.algorithm,
      expiresIn: this.config.signOptions.expiresIn,
      privateKey: this.config.privateKey,
    });
  }
}
