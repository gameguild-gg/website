import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { JobPostService } from './job-post.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobPostEntity } from './entities/job-post.entity';
import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
import { OwnershipEmptyInterceptor } from '../cms/interceptors/ownership-empty-interceptor.service';
import { WithRolesController } from 'src/cms/with-roles.controller';
import { CrudController, Crud } from '@dataui/crud';
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

}
