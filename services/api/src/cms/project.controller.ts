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
import { TicketEntity } from './entities/ticket.entity';

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
  // Justin, if you want to use a custom dto, you can change the types here
  dto: {
    create: CreateProjectDto,
    update: ProjectEntity,
    replace: ProjectEntity,
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

  @Override()
  @Auth(AuthenticatedRoute)
  async getMany(@ParsedRequest() req: CrudRequest): Promise<ProjectEntity[]> {
    return this.service.find({
      relations: ['owner', 'tickets'],
    });
  }
  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: CreateProjectDto })
  @ApiResponse({ type: ProjectEntity })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject() body: CreateProjectDto,
  ) {
    const res = await this.service.createOne(
      crudReq,
      body as Partial<ProjectEntity>,
    );
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
