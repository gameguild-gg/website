import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
import { Auth, ContentRoles } from '../auth/decorators/http.decorator';
import { GameEntity } from './entities/game.entity';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedBody,
  ParsedRequest,
} from '@dataui/crud';
import { GameDto } from './dtos/game.dto';
import { AuthUser } from '../auth';
import { UserEntity } from '../user/entities';

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
    createOneBase: {
      decorators: [Auth({ public: false })],
    },
    updateOneBase: {
      decorators: [Auth({ content: ContentRoles.EDITOR, entity: GameEntity })],
    },
    getOneBase: {
      decorators: [Auth({ public: true })],
    },
    getManyBase: {
      decorators: [Auth({ public: true })],
    },
    deleteOneBase: {
      decorators: [Auth({ content: ContentRoles.OWNER, entity: GameEntity })],
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
  createOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: GameDto,
    @AuthUser() user: UserEntity,
  ) {
    dto.owner = user;
    return this.base.createOneBase(req, <GameEntity>dto);
  }
}
