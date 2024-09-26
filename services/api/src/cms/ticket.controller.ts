import { Controller } from '@nestjs/common';
import {
  Crud,
  CrudController,
  CrudRequest,
  Override,
  ParsedRequest,
} from '@dataui/crud';
import { TicketService } from './ticket.service';
import { TicketEntity } from './entities/ticket.entity';
import { ApiBody, ApiTags } from '@nestjs/swagger';

import {
  AuthenticatedRoute,
  ManagerRoute,
  OwnerRoute,
} from '../auth/auth.enum';
import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
import { OwnershipEmptyInterceptor } from './interceptors/ownership-empty-interceptor.service';
import { ProjectEntity } from './entities/project.entity';
import { ProjectService } from './project.service';
import { Auth } from '../auth';

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
      decorators: [Auth<TicketEntity>(ManagerRoute<TicketEntity>)],
      interceptors: [OwnershipEmptyInterceptor],
    },
    deleteOneBase: {
      decorators: [Auth<TicketEntity>(OwnerRoute<TicketEntity>)],
    },
  },
})
@Controller('tickets')
@ApiTags('Ticket')
export class TicketController implements CrudController<TicketEntity> {
  constructor(
    public readonly service: TicketService,
    public readonly projectService: ProjectService,
  ) {}

  get base(): CrudController<TicketEntity> {
    return this;
  }

  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: TicketEntity })
  async createOne(
    @ParsedRequest() crudReq: CrudRequest,
    @BodyOwnerInject() body: TicketEntity, // You can also use a DTO here
  ) {
    const project = await this.projectService.findOne({
      where: { id: body.projectId },
    });

    // Create the ticket and associate it with the project
    const ticket = await this.base.createOneBase(crudReq, body);

    ticket.project = project;

    //project.tickets.push(ticket);

    //await this.projectService.save(project);

    return this.service.findOne({
      where: { id: ticket.id },
      relations: { owner: true, editors: true },
    });
  }
}
