import { Crud, CrudController } from '@dataui/crud';
import { Auth } from '../auth';
import { AuthenticatedRoute, EditorRoute, OwnerRoute, PublicRoute } from '../auth/auth.enum';
import { OwnershipEmptyInterceptor } from './interceptors/ownership-empty-interceptor.service';
import { Controller } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { WithRolesController } from './with-roles.controller';
import { CourseEntity } from './entities/course.entity';
import { CourseService } from './courses.service';

@Crud({
  model: {
    type: CourseEntity,
  },
  params: {
    slug: {
      field: 'slug',
      type: 'string',
      primary: true,
    },
  },
  dto: {
    // create: ,
  },
  routes: {
    exclude: ['replaceOneBase', 'createManyBase', 'createManyBase', 'recoverOneBase'],
    getOneBase: {
      decorators: [Auth(PublicRoute)],
    },
    createOneBase: {
      decorators: [Auth(AuthenticatedRoute)],
    },
    getManyBase: {
      decorators: [Auth(PublicRoute)],
    },
    updateOneBase: {
      decorators: [Auth<CourseEntity>(EditorRoute<CourseEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<CourseEntity>(OwnerRoute<CourseEntity>)],
    },
  },
})
@Controller('courses')
@ApiTags('courses')
export class CoursesController extends WithRolesController<CourseEntity> implements CrudController<CourseEntity> {
  constructor(public readonly service: CourseService) {
    super(service);
  }
}
