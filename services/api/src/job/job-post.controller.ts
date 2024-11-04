import { Body, Controller, Get, Logger, Param, UseInterceptors } from '@nestjs/common';
import { ApiBody, ApiResponse, ApiTags, getSchemaPath } from '@nestjs/swagger';
import { Swagger } from '@dataui/crud/lib/crud';
import { JobPostService } from './job-post.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobPostEntity } from './entities/job-post.entity';
import { AuthenticatedRoute, OwnerRoute } from '../auth/auth.enum';
import {
  OwnershipEmptyInterceptor,
  PartialWithoutFields,
} from '../cms/interceptors/ownership-empty-interceptor.service';
import { WithRolesController } from 'src/cms/with-roles.controller';
import {
  CrudController,
  Crud,
  Override,
  CrudRequest,
  ParsedRequest,
  CrudRequestInterceptor,
} from '@dataui/crud';
import { JobPostCreateDto } from './dtos/job-post-create.dto';
import { ExcludeFieldsPipe } from 'src/cms/pipes/exclude-fields.pipe';
import { BodyOwnerInject } from 'src/common/decorators/parameter.decorator';
import { JobPostWithAppliedDto } from './dtos/job-post-with-applied.dto';
import { UserEntity } from 'src/user/entities';
import { UserInject } from 'src/common/decorators/user-injection.decorator';
import { JobPostWithApplicationsDto } from './dtos/job-post-with-applications.dto';

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
      decorators: [Auth<JobPostEntity>(OwnerRoute<JobPostEntity>)],
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
      job_tags: {
        eager: true,
      },
    },
  },
})
@Controller('job-posts')
@ApiTags('Job Posts')
export class JobPostController
  extends WithRolesController<JobPostEntity>
  implements CrudController<JobPostEntity>
{
  private readonly logger = new Logger(JobPostController.name);

  constructor(public service: JobPostService) {
    super(service);
    // Modify custom function swagger manually for OpenAPIGenerator to create a proper API function
    const metadata = Swagger.getParams(this.getManyWithApplied);
    const queryParamsMeta = Swagger.createQueryParamsMeta('getManyBase', {
      model: { type: JobPostWithAppliedDto },
      query: { softDelete: false },
    });
    Swagger.setParams(
      [...metadata, ...queryParamsMeta],
      this.getManyWithApplied,
    );
  }

  get base(): CrudController<JobPostEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobPostCreateDto })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject(JobPostCreateDto) body: JobPostCreateDto,
  ) {
    return await this.service.createOneJob(crudReq, body);
  }

  @Override()
  @Auth<JobPostEntity>(OwnerRoute<JobPostEntity>)
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

  @Get('get-many-with-applied')
  @UseInterceptors(CrudRequestInterceptor)
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    status: 200,
    type: [JobPostWithAppliedDto],
    schema: { $ref: getSchemaPath(Array<JobPostWithAppliedDto>) },
  })
  async getManyWithApplied(
    @ParsedRequest() req: CrudRequest,
    @UserInject() user: UserEntity,
  ): Promise<JobPostWithAppliedDto[]> {
    return this.service.getManyWithApplied(req, user.id);
  }

  @Get('get-by-slug/:slug')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: JobPostEntity,
    schema: { $ref: getSchemaPath(JobPostEntity) },
    status: 200,
  })
  async getBySlug(
    @Param('slug') slug: string,
  ):Promise<JobPostEntity> {
    return this.service.getBySlug(slug)
  }

  @Get('get-by-slug-for-owner/:slug')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: JobPostEntity,
    schema: { $ref: getSchemaPath(JobPostEntity) },
    status: 200,
  })
  async getBySlugForOwner(
    @Param('slug') slug: string,
    @UserInject() user: UserEntity,
  ):Promise<JobPostWithApplicationsDto> {
    return this.service.getBySlugForOwner(slug, user.id)
  }
}
