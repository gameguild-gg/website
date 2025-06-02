// import { Body, ConflictException, Controller, Logger, Param } from '@nestjs/common';
// import { ApiBody, ApiResponse, ApiTags } from '@nestjs/swagger';
// import { ProjectService } from './project.service';
// import { Auth } from '../auth/decorators/http.decorator';
// import { ProjectEntity } from './entities/project.entity';
// import { Crud, CrudController, CrudRequest, Override, ParsedRequest } from '@dataui/crud';
// import { AuthenticatedRoute, EditorRoute, OwnerRoute } from '../auth/auth.enum';
// import { BodyOwnerInject } from '../common/decorators/parameter.decorator';
// import { OwnershipEmptyInterceptor } from './interceptors/ownership-empty-interceptor.service';
// import { WithRolesController } from './with-roles.controller';
// import { CreateProjectDto } from './dtos/create-project.dto';
//
// @Crud({
//   model: {
//     type: ProjectEntity,
//   },
//   params: {
//     slug: {
//       field: 'slug',
//       type: 'string',
//       primary: true,
//     },
//   },
//   dto: {
//     create: CreateProjectDto,
//   },
//   routes: {
//     exclude: ['replaceOneBase', 'createManyBase', 'createManyBase', 'recoverOneBase'],
//     getOneBase: {
//       decorators: [Auth(AuthenticatedRoute)],
//     },
//     createOneBase: {
//       decorators: [Auth(AuthenticatedRoute)],
//     },
//     getManyBase: {
//       decorators: [Auth(AuthenticatedRoute)],
//     },
//     updateOneBase: {
//       decorators: [Auth<ProjectEntity>(EditorRoute<ProjectEntity>)],
//       interceptors: [OwnershipEmptyInterceptor],
//     },
//     deleteOneBase: {
//       decorators: [Auth<ProjectEntity>(OwnerRoute<ProjectEntity>)],
//     },
//   },
// })
// @Controller('project')
// @ApiTags('Project')
// export class ProjectController extends WithRolesController<ProjectEntity> implements CrudController<ProjectEntity> {
//   private readonly logger = new Logger(ProjectController.name);
//
//   constructor(public readonly service: ProjectService) {
//     super(service);
//   }
//
//   get base(): CrudController<ProjectEntity> {
//     return this;
//   }
//
//   @Override()
//   @Auth(AuthenticatedRoute)
//   async getMany(@ParsedRequest() req: CrudRequest): Promise<ProjectEntity[]> {
//     // todo: check if this is working, it seems wrong, without paginations and filters
//     return this.service.find({
//       relations: { owner: true, tickets: true, versions: true, editors: true },
//     });
//   }
//
//   @Override()
//   @Auth(AuthenticatedRoute)
//   @ApiBody({ type: CreateProjectDto })
//   @ApiResponse({ type: ProjectEntity })
//   async createOne(@ParsedRequest() crudReq: CrudRequest, @BodyOwnerInject(CreateProjectDto) body: CreateProjectDto) {
//     const project = await this.service.findOne({
//       where: { slug: body.slug },
//     });
//     if (project) {
//       throw new ConflictException('Project with this slug already exists');
//     }
//
//     const res = await this.service.createOne(crudReq, body as Partial<ProjectEntity>);
//     return this.service.findOne({
//       where: { id: res.id },
//       relations: { owner: true, editors: true },
//     });
//   }
//
//   // override getOneBase to include relations
//   @Override()
//   @Auth(AuthenticatedRoute)
//   @ApiResponse({ type: ProjectEntity })
//   async getOne(@ParsedRequest() req: CrudRequest, @Param('slug') slug: string): Promise<ProjectEntity> {
//     return this.service.findOne({
//       where: { slug: slug },
//       relations: { owner: true, editors: true, versions: true, tickets: true },
//     });
//   }
//
//   @Override()
//   @Auth<ProjectEntity>(EditorRoute<ProjectEntity>)
//   @ApiBody({ type: CreateProjectDto })
//   @ApiResponse({ type: ProjectEntity })
//   async updateOne(@ParsedRequest() req: CrudRequest, @Body() dto: CreateProjectDto): Promise<ProjectEntity> {
//     return this.base.updateOneBase(req, dto);
//   }
// }
