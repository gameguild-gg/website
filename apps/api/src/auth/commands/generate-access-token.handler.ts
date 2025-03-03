import { Inject, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { JwtService } from '@nestjs/jwt';
import { accessTokenConfig } from '@/auth/config/access-token.config';
import { GenerateAccessTokenCommand } from '@/auth/commands/generate-access-token.command';
import { AccessTokenPayloadDto } from '@/auth/dtos/access-token-payload.dto';
import { TokenType } from '@/auth/dtos/token-type.enum';

@CommandHandler(GenerateAccessTokenCommand)
export class GenerateAccessTokenInHandler implements ICommandHandler<GenerateAccessTokenCommand> {
  private readonly logger = new Logger(GenerateAccessTokenInHandler.name);

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
