import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';// ../cms/
// import { ContentService } from '../cms/content.service';
import { JobService } from './job.service';
import { Auth } from '../auth/decorators/http.decorator';
// import { UserEntity } from '../user/entities';
import { JobPostEntity } from './entities/job-post.entity';
// import { OkResponse } from '../common/decorators/return-type.decorator';
// import { AuthType } from '../auth/guards';
import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
// import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
import { OwnershipEmptyInterceptor } from '../cms/interceptors/ownership-empty-interceptor.service';
import { WithRolesController } from 'src/cms/with-roles.controller';
import { CrudController, Crud } from '@dataui/crud';
// import { PartialWithoutFields } from '../cms/interceptors/ownership-empty-interceptor.service';
// import { ExcludeFieldsPipe } from 'src/cms/pipes/exclude-fields.pipe';
import { JobPostCreateDto } from './dtos/job-post-create.dto';

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
    getManyBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    updateOneBase: {
      decorators: [Auth<JobPostEntity>(ManagerRoute<JobPostEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<JobPostEntity>(OwnerRoute<JobPostEntity>)],
    },
  },
})
@Controller('jobs')
@ApiTags('Jobs')
export class JobController 
  extends WithRolesController<JobPostEntity>
  implements CrudController<JobPostEntity>
  {
  private readonly logger = new Logger(JobController.name);

  constructor(
    public service: JobService
  ) {
    super(service);
  }

}
