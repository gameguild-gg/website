import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
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
import { ContentUserRolesEnum } from '../auth/auth.enum';

@Crud({
  model: {
    type: GameEntity,
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
    deleteOneBase: {
      decorators: [
        Auth({ content: ContentUserRolesEnum.OWNER, entity: GameEntity }),
      ],
    },
  },
})
@Controller('game')
@ApiTags('game')
export class GameController implements CrudController<GameEntity> {
  private readonly logger = new Logger(GameController.name);

  constructor(public readonly service: GameService) {}

  get base(): CrudController<GameEntity> {
    return this;
  }

  @Override()
  @Auth({ public: false })
  createOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: GameEntity,
    @AuthUser() user: UserEntity,
  ) {
    dto.roles = {
      owner: user.id,
      editors: [user.id],
    };
    return this.base.createOneBase(req, <GameEntity>dto);
  }

  @Override()
  @Auth({ content: ContentUserRolesEnum.EDITOR, entity: GameEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: Partial<GameEntity>,
  ): Promise<GameEntity> {
    return this.base.updateOneBase(req, <GameEntity>dto);
  }
}