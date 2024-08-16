import { Controller, Logger, UnauthorizedException } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { Auth } from '../auth/decorators/http.decorator';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedBody,
  ParsedRequest,
} from '@dataui/crud';
import { GameVersionEntity } from './entities/game-version.entity';
import { GameVersionService } from './game-version.service';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { GameService } from './game.service';
import { UserEntity } from '../user/entities';
import { AuthUser } from '../auth';

@Crud({
  model: {
    type: GameVersionEntity,
  },
  params: {
    id: {
      field: 'id',
      type: 'uuid',
      primary: true,
    },
  },
  routes: {
    exclude: [
      'replaceOneBase',
      'updateOneBase',
      'createManyBase',
      'recoverOneBase',
    ],
    getOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    getManyBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
  },
})
@Controller('game-version')
@ApiTags('game-version')
export class GameVersionController
  implements CrudController<GameVersionEntity>
{
  private readonly logger = new Logger(GameVersionController.name);

  constructor(
    public readonly service: GameVersionService,
    private gameService: GameService,
  ) {}

  get base(): CrudController<GameVersionEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  async createOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: GameVersionEntity,
    @AuthUser() user: UserEntity,
  ): Promise<GameVersionEntity> {
    if (!dto.game || !dto.game.id) {
      throw new UnauthorizedException('game.id field is required');
    }
    // todo: move this to a decorator
    if (await this.gameService.UserCanEdit(user.id, dto.game.id))
      return this.base.createOneBase(req, <GameVersionEntity>dto);
    else
      throw new UnauthorizedException(
        'User does not have permission to edit this game',
      );
  }

  @Override()
  @Auth(AuthenticatedRoute)
  async deleteOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: GameVersionEntity,
    @AuthUser() user: UserEntity,
  ): Promise<void | GameVersionEntity> {
    if (!dto.game || !dto.game.id) {
      throw new UnauthorizedException('game.id field is required');
    }

    // todo: move this to a decorator
    if (await this.gameService.UserCanEdit(user.id, dto.game.id))
      return this.base.deleteOneBase(req);
    else
      throw new UnauthorizedException(
        'User does not have permission to delete this game',
      );
  }
}
