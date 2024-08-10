import { Injectable, Logger } from '@nestjs/common';
import { GameEntity } from './entities/game.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { TypeOrmCrudService } from '@dataui/crud-typeorm';

@Injectable()
export class GameService extends TypeOrmCrudService<GameEntity> {
  private readonly logger = new Logger(GameService.name);
  constructor(
    @InjectRepository(GameEntity)
    private readonly gameRepository: Repository<GameEntity>,
  ) {
    super(gameRepository);
  }
}
