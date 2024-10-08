import { Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { JobAplicationService } from './job-aplication.service';
import { Auth } from '../auth/decorators/http.decorator';
import { JobAplicationEntity } from './entities/job-aplication.entity';
import { AuthenticatedRoute } from '../auth/auth.enum';
import { CrudController, Crud } from '@dataui/crud';

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

}
