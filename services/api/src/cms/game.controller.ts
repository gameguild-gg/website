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
import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
import {
  OwnershipEmptyInterceptor,
  PartialWithoutFields,
} from './interceptors/ownership-empty-interceptor.service';
import { ExcludeFieldsPipe } from './pipes/exclude-fields.pipe';
import { WithRolesController } from './with-roles.controller';

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
      decorators: [Auth(AuthenticatedRoute)],
    },
    createOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    getManyBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    updateOneBase: {
      decorators: [Auth<GameEntity>(ManagerRoute<GameEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<GameEntity>(OwnerRoute<GameEntity>)],
    },
  },
})
@Controller('game')
@ApiTags('game')
export class GameController
  extends WithRolesController<GameEntity>
  implements CrudController<GameEntity>
{
  private readonly logger = new Logger(GameController.name);

  constructor(public readonly service: GameService) {
    super(service);
  }

  get base(): CrudController<GameEntity> {
    return this;
  }

  // we need to override to guarantee the user is being injected as owner and editor
  @Override()
  @Auth(AuthenticatedRoute)
  createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject() body: GameEntity,
  ) {
    return this.base.createOneBase(crudReq, body);
  }

  @Override()
  @Auth<GameEntity>(ManagerRoute<GameEntity>)
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(new ExcludeFieldsPipe<GameEntity>(['owner', 'editors']))
    dto: PartialWithoutFields<GameEntity, 'owner' | 'editors'>,
  ): Promise<GameEntity> {
    return this.base.updateOneBase(req, dto);
  }
}
