import { Body, Controller, Logger, Post } from '@nestjs/common';
import { ApiOkResponse, ApiTags } from '@nestjs/swagger';
import { GameService } from './game.service';
import { GameDto } from './dtos/game.dto';
import { Auth, ContentRoles } from '../auth/decorators/http.decorator';
import { AuthUser } from '../auth';
import { UserEntity } from '../user/entities';
import { ContentBaseDto } from './dtos/content.base.dto';
import { GameEntity } from './entities/game.entity';
import { Crud } from '@dataui/crud';
import { IsOwner } from '../auth/decorators/owner.decorator';
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
    exclude: ['replaceOneBase', 'createManyBase'],
    createOneBase: {
      decorators: [Auth({ public: false })],
    },
    updateOneBase: {
      decorators: [Auth({ content: ContentRoles.EDITOR })],
    },
    getOneBase: {
      decorators: [Auth({ public: true })],
    },
    getManyBase: {
      decorators: [Auth({ public: true })],
    },
    deleteOneBase: {
      decorators: [Auth({ content: ContentRoles.OWNER })],
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
