import { Body, Controller, Logger, Post } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
import { GameDto } from './dtos/game.dto';
import { Auth } from '../auth/decorators/http.decorator';
import { AuthUser } from '../auth';
import { UserEntity } from '../user/entities';
import { ContentBaseDto } from './dtos/content.base.dto';
import { GameEntity } from './entities/game.entity';
import { Crud } from '@dataui/crud';
import { IsOwner } from '../common/decorators/isowner.decorator';
import { IsOwnerInterceptor } from '../common/interceptors/isowner.interceptor';

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
    exclude: ['replaceOneBase', 'deleteOneBase', 'createManyBase'],
    createOneBase: {
      decorators: [Auth()],
    },
    updateOneBase: {
      decorators: [Auth(), IsOwner(GameEntity)],
      interceptors: [IsOwnerInterceptor],
    },
    getOneBase: {
      decorators: [Auth()],
    },
    getManyBase: {
      decorators: [Auth()],
    },
  },
})
@Controller('game')
@ApiTags('game')
export class GameController {
  private readonly logger = new Logger(GameController.name);

  constructor(private readonly service: GameService) {}

  // CRUD GAME
  // @Post('create')
  // @Auth()
  // @ApiOkResponse({ type: GameDto })
  // async createGame(
  //   @AuthUser() user: UserEntity,
  //   @Body() game: ContentBaseDto,
  // ): Promise<GameDto> {
  //   return this.service.createGame(game, user);
  // }
  //
  // @Post('update')
  // @Auth()
  // @ApiOkResponse({ type: GameDto })
  // async updateGame(
  //   @AuthUser() user: UserEntity,
  //   @Body() game: Partial<ContentBaseDto>,
  // ): Promise<GameDto> {
  //   return this.service.updateGame(game, user);
  // }
  //
  // @Post('find')
  // @Auth()
  // @ApiOkResponse({ type: GameDto, isArray: true })
  // async findGame(
  //   @AuthUser() user: UserEntity,
  //   @Body() game: Partial<ContentBaseDto>,
  // ): Promise<GameDto> {
  //   return this.service.findGame(game, user);
  // }
}
