import { Controller, Body, ForbiddenException, Param, Logger } from '@nestjs/common';
import { Crud, CrudController, CrudRequest, Override, ParsedRequest } from '@dataui/crud';
import { TicketService } from './ticket.service';
import { TicketEntity } from './entities/ticket.entity';
import { ApiBody, ApiTags } from '@nestjs/swagger';
import { CreateTicketDto } from './dtos/create-ticket.dto';
import { AuthenticatedRoute, OwnerRoute } from '../auth/auth.enum';
import { OwnershipEmptyInterceptor } from './interceptors/ownership-empty-interceptor.service';
import { ProjectService } from './project.service';
import { Auth } from '../auth';

@Crud({
  model: {
    type: TicketEntity,
  },
  params: {
    id: {
      field: 'id',
      type: 'uuid',
      primary: true,
    },
  },
  dto: { create: CreateTicketDto }, // The DTO is still valid here for automatic routes
  routes: {
    exclude: ['replaceOneBase', 'createManyBase', 'createManyBase', 'recoverOneBase'],
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
      decorators: [Auth<TicketEntity>(OwnerRoute<TicketEntity>)],
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

  logger: Logger = new Logger(TicketController.name);

  get base(): CrudController<TicketEntity> {
    return this;
  }
  @Override()
  @Auth(AuthenticatedRoute)
  async getMany(@ParsedRequest() req: CrudRequest): Promise<TicketEntity[]> {
    return this.service.find({
      relations: { owner: true, project: true },
    });
  }
  @Override()
  @Auth(AuthenticatedRoute)
  async getOne(@ParsedRequest() req: CrudRequest, @Param('id') id: string): Promise<TicketEntity> {
    return this.service.findOne({
      where: { id: id },
      relations: { owner: true, project: true },
    });
  }
  @Override()
  @Auth(AuthenticatedRoute)
  @ApiBody({ type: CreateTicketDto }) // For Swagger documentation
  async createOne(@ParsedRequest() crudReq: CrudRequest, @Body() createTicketDto: CreateTicketDto) {
    if (createTicketDto.owner != undefined) {
      const project = await this.projectService.findOne({
        where: { id: createTicketDto.projectId },
      });

      const ticketEntity: TicketEntity = {
        ...new TicketEntity(),
        ...createTicketDto,
      };

      ticketEntity.project = project;

      const ticket = await this.base.createOneBase(crudReq, ticketEntity);

      await this.projectService.save(project);

      return this.service.findOne({
        where: { id: ticket.id },
        relations: { owner: true, editors: true },
      });
    } else {
      this.logger.log('Owner undffined Please try again');
      throw new ForbiddenException();
    }
  }
}
