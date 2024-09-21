import {
  Body,
  Controller,
  Logger,
  UnauthorizedException,
} from '@nestjs/common';
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
import { ProjectVersionEntity } from './entities/project-version.entity';
import { ProjectVersionService } from './project-version.service';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { ProjectService } from './project.service';
import { UserEntity } from '../user/entities';
import { AuthUser } from '../auth';
import { ExcludeFieldsPipe } from './pipes/exclude-fields.pipe';
import { PartialWithoutFields } from './interceptors/ownership-empty-interceptor.service';

@Crud({
  model: {
    type: ProjectVersionEntity,
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
export class ProjectVersionController
  implements CrudController<ProjectVersionEntity>
{
  private readonly logger = new Logger(ProjectVersionController.name);

  constructor(
    public readonly service: ProjectVersionService,
    private gameService: ProjectService,
  ) {}

  get base(): CrudController<ProjectVersionEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  async createOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() parsedBody: ProjectVersionEntity,
    @Body(
      new ExcludeFieldsPipe<ProjectVersionEntity>([
        'createdAt',
        'updatedAt',
        'id',
        'responses',
      ]),
    )
    dto: PartialWithoutFields<
      ProjectVersionEntity,
      'createdAt' | 'updatedAt' | 'id' | 'responses'
    >,
    @AuthUser() user: UserEntity,
  ): Promise<ProjectVersionEntity> {
    if (!dto.project || !dto.project.id) {
      throw new UnauthorizedException('game.id field is required');
    }
    // todo: move this to a decorator
    if (await this.gameService.UserCanEdit(user.id, dto.project.id))
      return this.base.createOneBase(req, <ProjectVersionEntity>dto);
    else
      throw new UnauthorizedException(
        'User does not have permission to edit this game',
      );
  }

  @Override()
  @Auth(AuthenticatedRoute)
  async deleteOne(
    @ParsedRequest() req: CrudRequest,
    @ParsedBody() dto: ProjectVersionEntity,
    @AuthUser() user: UserEntity,
  ): Promise<void | ProjectVersionEntity> {
    if (!dto.project || !dto.project.id) {
      throw new UnauthorizedException('game.id field is required');
    }

    // todo: move this to a decorator
    if (await this.gameService.UserCanEdit(user.id, dto.project.id))
      return this.base.deleteOneBase(req);
    else
      throw new UnauthorizedException(
        'User does not have permission to delete this game',
      );
  }
}
