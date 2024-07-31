import { Body, Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { Auth } from '../auth/decorators/http.decorator';
import { GameEntity } from './entities/game.entity';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedBody,
  ParsedRequest,
} from '@dataui/crud';
import { AuthUser } from '../auth';
import { UserEntity } from '../user/entities';
import { GameVersionEntity } from './entities/game-version.entity';
import { GameVersionService } from './game-version.service';
import { ContentUserRolesEnum } from '../auth/auth.enum';

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
    exclude: ['replaceOneBase', 'createManyBase'],
    getOneBase: {
      decorators: [Auth({ public: true })],
    },
    getManyBase: {
      decorators: [Auth({ public: true })],
    },
  },
})
@Controller('game-version')
@ApiTags('game-version')
export class GameVersionController
  implements CrudController<GameVersionEntity>
{
  private readonly logger = new Logger(GameVersionController.name);

  constructor(public readonly service: GameVersionService) {}

  get base(): CrudController<GameVersionEntity> {
    return this;
  }

  @Override()
  @Auth({ public: false })
  async createOne(
    @Body() body: GameVersionEntity,
    @AuthUser() user: UserEntity,
  ): Promise<GameVersionEntity> {
    return this.service.createGameVersion(body, user);
  }

  @Override()
  @Auth({ content: ContentUserRolesEnum.EDITOR, entity: GameEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: Partial<GameEntity>,
  ) {
    delete dto.roles;
    await this.base.updateOneBase(req, <GameEntity>dto);
  }

  // @Override()
  // @Auth({ public: false })
  // deleteOne(
}
