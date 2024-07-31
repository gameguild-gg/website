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
  //
  // async createGame(
  //   content: ContentBaseDto,
  //   user: UserEntity,
  // ): Promise<GameEntity> {
  //   const game = this.gameRepository.create(content);
  //   game.owner = user;
  //   return this.gameRepository.save(game);
  // }
  //
  // async updateGame(
  //   content: Partial<GameDto>,
  //   user: UserEntity,
  // ): Promise<GameEntity> {
  //   const game = await this.gameRepository.findOne({
  //     where: { id: content.id },
  //     relations: { owner: true, editors: true },
  //   });
  //   // todo: mowe this logic of ownership and editor to elsewhere
  //   if (!game) {
  //     throw new NotFoundException('Game id: ' + content.id + ' not found');
  //   }
  //   if (
  //     game.owner.id !== user.id && // user is not the owner
  //     !game.editors.find((e) => e.id === user.id) // user is not an editor
  //   ) {
  //     throw new NotFoundException('User is not the owner of the game');
  //   }
  //   return this.gameRepository.save({ ...content });
  // }
}
