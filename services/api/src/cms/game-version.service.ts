import {
  Injectable,
  Logger,
  NotFoundException,
  UnauthorizedException,
} from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { ArrayContains, Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { GameVersionEntity } from './entities/game-version.entity';
import { GameVersionDto } from './dtos/game-version.dto';
import { UserEntity } from '../user/entities';
import { GameEntity } from './entities/game.entity';

@Injectable()
export class GameVersionService extends TypeOrmCrudService<GameVersionEntity> {
  private readonly logger = new Logger(GameVersionService.name);
  constructor(
    @InjectRepository(GameVersionEntity)
    private readonly gameVersionRepository: Repository<GameVersionEntity>,
    @InjectRepository(GameEntity)
    private readonly gameRepository: Repository<GameEntity>,
  ) {
    super(gameVersionRepository);
  }

  async createGameVersion(
    body: GameVersionDto,
    user: UserEntity,
  ): Promise<GameVersionEntity> {
    if (!body.game || !body.game.id) {
      throw new NotFoundException('{ game: { id: string } } field is required');
    }
    // check if user is owner or editor of the game
    const game = await this.gameRepository.findOne({
      where: [
        // is owner
        { id: body.game.id, roles: { owner: user.id } },
        // or is one of the editors
        { id: body.game.id, roles: { editors: ArrayContains([user.id]) } },
      ],
    });
    // check if the user can create a version of the game
    if (!game) {
      throw new UnauthorizedException(
        'User is not the owner or editor of the game',
      );
    }
    // create the version
    const version = this.gameVersionRepository.create(body);
    version.game = game;
    return this.gameVersionRepository.save(version);
  }
}
