import { Body, Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
import { Auth } from '../auth/decorators/http.decorator';
import { GameEntity } from './entities/game.entity';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedRequest,
} from '@dataui/crud';
import { ContentUserRolesEnum } from '../auth/auth.enum';
import { AuthType } from '../auth/guards';
import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
import {
  OwnershipEmptyInterceptor,
  PartialWithoutFields,
} from './interceptors/ownership-empty-interceptor.service';
import { ExcludeFieldsPipe } from './pipes/exclude-fields.pipe';

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
    exclude: [
      'replaceOneBase',
      'createManyBase',
      'createManyBase',
      'recoverOneBase',
    ],
    getOneBase: {
      decorators: [Auth({ guard: AuthType.AccessToken })],
    },
    createOneBase: {
      decorators: [Auth({ guard: AuthType.AccessToken })],
    },
    getManyBase: {
      decorators: [Auth({ guard: AuthType.AccessToken })],
    },
    updateOneBase: {
      decorators: [
        Auth({ content: ContentUserRolesEnum.EDITOR, entity: GameEntity }),
      ],
      interceptors: [OwnershipEmptyInterceptor],
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

  // we need to override to guarantee the user is being injected as owner and editor
  @Override()
  @Auth({ guard: AuthType.AccessToken })
  createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject() body: GameEntity,
  ) {
    return this.base.createOneBase(crudReq, body);
  }

  @Override()
  @Auth({ content: ContentUserRolesEnum.EDITOR, entity: GameEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(new ExcludeFieldsPipe<GameEntity>(['owner', 'editors']))
    dto: PartialWithoutFields<GameEntity, 'owner' | 'editors'>,
  ): Promise<GameEntity> {
    return this.base.updateOneBase(req, dto);
  }
}
