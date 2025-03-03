import { Inject, Logger } from '@nestjs/common';
import { ConfigType } from '@nestjs/config';
import { CommandHandler, EventPublisher, ICommandHandler } from '@nestjs/cqrs';
import { JwtService } from '@nestjs/jwt';
import { refreshTokenConfig } from '@/auth/config/refresh-token.config';
import { GenerateRefreshTokenCommand } from '@/auth/commands/generate-refresh-token.command';
import { RefreshTokenPayloadDto } from '@/auth/dtos/refresh-token-payload.dto';
import { TokenType } from '@/auth/dtos/token-type.enum';

@CommandHandler(GenerateRefreshTokenCommand)
export class GenerateRefreshTokenInHandler implements ICommandHandler<GenerateRefreshTokenCommand> {
  private readonly logger = new Logger(GenerateRefreshTokenInHandler.name);

  constructor(
    @Inject(refreshTokenConfig.KEY)
    private readonly config: ConfigType<typeof refreshTokenConfig>,
    private readonly jwtService: JwtService,
    private readonly publisher: EventPublisher,
  ) {}

  public async execute(command: GenerateRefreshTokenCommand): Promise<string> {
    const { user } = command;

    const payload: RefreshTokenPayloadDto = {
      sub: user.id,
      // email: user.email,
      // username: user.username,
      // wallet: user.walletAddress,
      type: TokenType.RefreshToken,
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
