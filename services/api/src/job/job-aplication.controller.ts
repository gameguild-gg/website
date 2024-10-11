import { Body, Controller, Logger } from '@nestjs/common';
import { ApiBody, ApiTags } from '@nestjs/swagger';
import { JobAplicationService } from './job-aplication.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { CrudController, Crud, CrudRequest, Override, ParsedRequest } from '@dataui/crud';
import { PartialWithoutFields } from 'src/cms/interceptors/ownership-empty-interceptor.service';
import { ExcludeFieldsPipe } from 'src/cms/pipes/exclude-fields.pipe';
import { BodyAplicantInject } from './decorators/body-aplicant-injection.decorator';
import { JobAplicationCreateDto } from './dtos/job-aplication-create.dto';

@Crud({
  model: {
    type: JobAplicationEntity,
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
})
@Controller('job-aplications')
@ApiTags('Job Aplications')
export class JobAplicationController
  implements CrudController<JobAplicationEntity>
  {
  private readonly logger = new Logger(JobAplicationController.name);

  constructor(
    public service: JobAplicationService
  ) { }

  get base(): CrudController<JobAplicationEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobAplicationCreateDto })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    // todo: remove id and other unwanted fields
    @BodyAplicantInject() body: JobAplicationEntity,
  ) {
    const res = await this.base.createOneBase(crudReq, body);
    return this.service.findOne({
      where: { id: res.id },
      relations: { aplicant: true },
    });
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: JobAplicationEntity })
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(
      new ExcludeFieldsPipe<JobAplicationEntity>([
        'createdAt',
        'updatedAt',
      ]),
    )
    dto: PartialWithoutFields<
    JobAplicationEntity,
      'createdAt' | 'updatedAt'
    >,
  ): Promise<JobAplicationEntity> {
    return this.base.updateOneBase(req, dto);
  }

}
