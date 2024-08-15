import { Body, Controller, Logger } from '@nestjs/common';
import { ApiTags } from '@nestjs/swagger';
import { Auth } from '../../../auth/decorators/http.decorator';
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
} from '../../../auth/auth.enum';
import { BodyOwnerInject } from '../../../common/decorators/parameter.decorator';
import {
  OwnershipEmptyInterceptor,
  PartialWithoutFields,
} from '../../interceptors/ownership-empty-interceptor.service';
import { ExcludeFieldsPipe } from '../../pipes/exclude-fields.pipe';
import { WithRolesController } from '../../with-roles.controller';
import { CourseEntity } from '../../entities/course.entity';
import { CourseService } from './course.service';

@Crud({
  model: {
    type: CourseEntity,
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
      decorators: [Auth<CourseEntity>(ManagerRoute<CourseEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<CourseEntity>(OwnerRoute<CourseEntity>)],
    },
  },
})
@Controller('course')
@ApiTags('course')
export class CourseController
  extends WithRolesController<CourseEntity>
  implements CrudController<CourseEntity>
{
  private readonly logger = new Logger(CourseController.name);

  constructor(public readonly service: CourseService) {
    super(service);
  }

  get base(): CrudController<CourseEntity> {
    return this;
  }

  // we need to override to guarantee the user is being injected as owner and editor
  @Override()
  @Auth(AuthenticatedRoute)
  createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject() body: CourseEntity,
  ) {
    return this.base.createOneBase(crudReq, body);
  }

  @Override()
  @Auth<CourseEntity>(ManagerRoute<CourseEntity>)
  async updateOne(
    @ParsedRequest() req: CrudRequest,
    @Body(new ExcludeFieldsPipe<CourseEntity>(['owner', 'editors']))
    dto: PartialWithoutFields<CourseEntity, 'owner' | 'editors'>,
  ): Promise<CourseEntity> {
    return this.base.updateOneBase(req, dto);
  }
}
