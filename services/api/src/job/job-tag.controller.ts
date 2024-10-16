import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { JobTagService } from './job-tag.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobTagEntity } from './entities/job-tag.entity';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { CrudController, Crud } from '@dataui/crud';

@Crud({
  model: {
    type: JobTagEntity,
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
@Controller('job-tags')
@ApiTags('Job Tags')
export class JobTagController
  implements CrudController<JobTagEntity>
  {
  private readonly logger = new Logger(JobTagController.name);

  constructor(
    public service: JobTagService
  ) { }

}
