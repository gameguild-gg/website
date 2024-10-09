import { Body, Controller, Logger } from '@nestjs/common';
import { ApiBody, ApiTags } from '@nestjs/swagger';
import { JobPostService } from './job-post.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobPostEntity } from './entities/job-post.entity';
import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
import { OwnershipEmptyInterceptor, PartialWithoutFields } from '../cms/interceptors/ownership-empty-interceptor.service';
import { WithRolesController } from 'src/cms/with-roles.controller';
import { CrudController, Crud, Override, CrudRequest, ParsedRequest } from '@dataui/crud';
import { JobPostCreateDto } from './dtos/job-post-create.dto';
import { ExcludeFieldsPipe } from 'src/cms/pipes/exclude-fields.pipe';
import { BodyOwnerInject } from 'src/common/decorators/parameter.decorator';

@Crud({
  model: {
    type: JobPostEntity,
  },
  params: {
    id: {
      field: 'id',
      type: 'uuid',
      primary: true,
    },
  },
  dto: {
    create: JobPostCreateDto,   
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
    // getManyBase: {
    //   decorators: [Auth(AuthenticatedRoute)],
    // },
    updateOneBase: {
      decorators: [Auth<JobPostEntity>(ManagerRoute<JobPostEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<JobPostEntity>(OwnerRoute<JobPostEntity>)],
    },
  },
  query: {
    join: {
      owner: {
        eager: true,
      },
      tags: {
        eager: true,
      }
    }
  }
})
@Controller('job-posts')
@ApiTags('Job Posts')
export class JobPostController 
  extends WithRolesController<JobPostEntity>
  implements CrudController<JobPostEntity>
  {
  private readonly logger = new Logger(JobPostController.name);

  constructor(
    public service: JobPostService
  ) {
    super(service);
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobPostCreateDto })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    // todo: remove id and other unwanted fields
    @BodyOwnerInject() body: JobPostEntity,
  ) {
    const res = await this.base.createOneBase(crudReq, body);
    return this.service.findOne({
      where: { id: res.id },
      relations: { owner: true, editors: true },
    });
  }

  get base(): CrudController<JobPostEntity> {
    return this;
  }

  @Override()
  @Auth<JobPostEntity>(ManagerRoute<JobPostEntity>)
  @ApiBody({ type: JobPostEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(
      new ExcludeFieldsPipe<JobPostEntity>([
        'owner',
        'editors',
        'createdAt',
        'updatedAt',
      ]),
    )
    dto: PartialWithoutFields<
      JobPostEntity,
      'owner' | 'editors' | 'createdAt' | 'updatedAt'
    >,
  ): Promise<JobPostEntity> {
    return this.base.updateOneBase(req, dto);
  }

}
