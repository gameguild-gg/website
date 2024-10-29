import { Body, Controller, Get, Logger, Param, Post } from '@nestjs/common';
import { ApiBody, ApiResponse, ApiTags, getSchemaPath } from '@nestjs/swagger';
import { JobApplicationService } from './job-application.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobApplicationEntity } from './entities/job-application.entity';
import { AuthenticatedRoute } from '../auth/auth.enum';
import {
  CrudController,
  Crud,
  CrudRequest,
  Override,
  ParsedRequest,
} from '@dataui/crud';
import { PartialWithoutFields } from 'src/cms/interceptors/ownership-empty-interceptor.service';
import { ExcludeFieldsPipe } from 'src/cms/pipes/exclude-fields.pipe';
import { BodyApplicantInject } from './decorators/body-applicant-injection.decorator';
import { JobAplicationCreateDto } from './dtos/job-aplication-create.dto';
import { UserInject } from 'src/common/decorators/user-injection.decorator';
import { UserEntity } from 'src/user/entities';

@Crud({
  model: {
    type: JobApplicationEntity,
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
      decorators: [Auth(AuthenticatedRoute)],
    },
    deleteOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
  },
  query: {
    join: {
      applicant: {
        eager: true,
      },
      job: {
        eager: true,
      }
    }
  }
})
@Controller('job-applications')
@ApiTags('Job Applications')
export class JobApplicationController
  implements CrudController<JobApplicationEntity>
{
  private readonly logger = new Logger(JobApplicationController.name);

  constructor(public service: JobApplicationService) {}

  get base(): CrudController<JobApplicationEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobAplicationCreateDto })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    // todo: remove id and other unwanted fields
    @BodyApplicantInject() body: JobApplicationEntity,
  ) {
    const res = await this.base.createOneBase(crudReq, body);
    return this.service.findOne({
      where: { id: res.id },
      relations: { applicant: true },
    });
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobApplicationEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(
      new ExcludeFieldsPipe<JobApplicationEntity>(['createdAt', 'updatedAt']),
    )
    dto: PartialWithoutFields<JobApplicationEntity, 'createdAt' | 'updatedAt'>,
  ): Promise<JobApplicationEntity> {
    return this.base.updateOneBase(req, dto);
  }

  @Get('my-applications')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: Promise<JobApplicationEntity[]>,
    schema: { $ref: getSchemaPath(Array<JobApplicationEntity>) },
  })
  async myApplications(
    @UserInject() user: UserEntity,
  ): Promise<JobApplicationEntity[]> {
    return this.service.myApplications(user.id);
  }

  @Get('my-application-by-slug/:slug')
  @Auth(AuthenticatedRoute)
  @ApiResponse({
    type: Promise<JobApplicationEntity[]>,
    schema: { $ref: getSchemaPath(JobApplicationEntity) },
  })
  async myApplicationBySlug(
    @Param('slug') slug: string,
    @UserInject() user: UserEntity,
  ): Promise<JobApplicationEntity> {
    return this.service.myApplicationBySlug(slug, user.id);
  }

  @Post('advance-candidate')
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobApplicationEntity })
  async advanceCandidate(
    @Body() application: JobApplicationEntity,
    @UserInject() jobManager: UserEntity,
  ):Promise<JobApplicationEntity> {
    return this.service.advanceCandidate(application, jobManager.id);
  }

  @Post('undo-advance-candidate')
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobApplicationEntity })
  async moveBackCandidate(
    @Body() application: JobApplicationEntity,
    @UserInject() jobManager: UserEntity,
  ):Promise<JobApplicationEntity> {
    return this.service.undoAdvanceCandidate(application, jobManager.id);
  }

  @Post('reject-candidate')
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobApplicationEntity })
  async rejectCandidate(
    @Body() application: JobApplicationEntity,
    @UserInject() jobManager: UserEntity,
  ):Promise<JobApplicationEntity> {
    return this.service.rejectCandidate(application, jobManager.id);
  }

  @Post('undo-reject-candidate')
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobApplicationEntity })
  async undoRejectCandidate(
    @Body() application: JobApplicationEntity,
    @UserInject() jobManager: UserEntity,
  ):Promise<JobApplicationEntity> {
    return this.service.undoRejectCandidate(application, jobManager.id);
  }
}
