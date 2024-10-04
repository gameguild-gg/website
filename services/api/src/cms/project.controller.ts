import { Body, Controller, Get, Logger } from '@nestjs/common';
import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
import { ProjectService } from './project.service';
import { Auth } from '../auth/decorators/http.decorator';
import { ProjectEntity } from './entities/project.entity';
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
import { CreateProjectDto } from './dtos/Create-Project.dto';

@Crud({
  model: {
    type: ProjectEntity,
  },
  dto: {
    create: CreateProjectDto,
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
      decorators: [Auth<ProjectEntity>(ManagerRoute<ProjectEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<ProjectEntity>(OwnerRoute<ProjectEntity>)],
    },
  },
})
@Controller('project')
@ApiTags('Project')
export class ProjectController
  extends WithRolesController<ProjectEntity>
  implements CrudController<ProjectEntity>
{
  private readonly logger = new Logger(ProjectController.name);

  constructor(public readonly service: ProjectService) {
    super(service);
  }

  get base(): CrudController<ProjectEntity> {
    return this;
  }

  // we need to override to guarantee the user is being injected as owner and editor
  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: ProjectEntity })
  @ApiResponse({ type: ProjectEntity })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    // todo: remove id and other unwanted fields
    @BodyOwnerInject() body: ProjectEntity,
  ) {
    const res = await this.base.createOneBase(crudReq, body);
    return this.service.findOne({
      where: { id: res.id },
      relations: { owner: true, editors: true },
    });
  }

  @Override()
  @Auth<ProjectEntity>(ManagerRoute<ProjectEntity>)
  @ApiBody({ type: ProjectEntity })
  @ApiResponse({ type: ProjectEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(
      new ExcludeFieldsPipe<ProjectEntity>([
        'owner',
        'editors',
        'versions',
        'createdAt',
        'updatedAt',
      ]),
    )
    dto: PartialWithoutFields<
      ProjectEntity,
      'owner' | 'editors' | 'versions' | 'createdAt' | 'updatedAt'
    >,
  ): Promise<ProjectEntity> {
    return this.base.updateOneBase(req, dto);
  }
}
