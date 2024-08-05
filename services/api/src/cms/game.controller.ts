import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
import { Auth } from '../auth/decorators/http.decorator';
import { GameEntity } from './entities/game.entity';
import { Crud, CrudController } from '@dataui/crud';
import { ContentUserRolesEnum } from '../auth/auth.enum';
import { AuthType } from '../auth/guards';

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
      decorators: [Auth({ guard: AuthType.AccessToken })],
    },
    createOneBase: {
      decorators: [Auth({ guard: AuthType.AccessToken, injectOwner: true })],
    },
    getManyBase: {
      decorators: [Auth({ guard: AuthType.AccessToken })],
    },
    updateOneBase: {
      decorators: [
        Auth({ content: ContentUserRolesEnum.EDITOR, entity: GameEntity }),
      ],
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
}
