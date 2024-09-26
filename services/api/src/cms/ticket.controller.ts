import {
  Controller,
  Post,
  Put,
  Body,
  Param,
  Get,
  Delete,
} from '@nestjs/common';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedRequest,
} from '@dataui/crud';
import { TicketService } from './ticket.service';
import { TicketEntity, TicketStatus } from './entities/ticket.entity';
import { CreateTicketDto } from './dtos/Create-Ticket.dto';
import { ApiTags } from '@nestjs/swagger';

import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
import { OwnershipEmptyInterceptor } from './interceptors/ownership-empty-interceptor.service';
import { ProjectEntity } from './entities/project.entity';
import { Auth } from '../auth';
import { WithRolesEntity } from '../auth/entities/with-roles.entity';
import { WithRolesController } from './with-roles.controller';

@Crud({
  model: {
    type: ProjectEntity,
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
      decorators: [Auth<ProjectEntity>(ManagerRoute<ProjectEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<ProjectEntity>(OwnerRoute<ProjectEntity>)],
    },
  },
})
@Controller('tickets')
@ApiTags('Ticket')
export class TicketController implements CrudController<TicketEntity> {
  constructor(public readonly service: TicketService) {
    super(service);
  }

  get base(): CrudController<TicketEntity> {
    return this;
  }
}
