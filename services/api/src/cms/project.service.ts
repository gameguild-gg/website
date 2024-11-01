import { Injectable, Logger } from '@nestjs/common';
import { ProjectEntity } from './entities/project.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { WithRolesService } from './with-roles.service';
//import { Api } from '@game-guild/apiclient';
//import ProjectVersionEntity = Api.ProjectVersionEntity;

@Injectable()
export class ProjectService extends WithRolesService<ProjectEntity> {
  private readonly logger = new Logger(ProjectService.name);
  constructor(
    @InjectRepository(ProjectEntity)
    private readonly gameRepository: Repository<ProjectEntity>,
  ) {
    super(gameRepository);
  }
  save(project: ProjectEntity): Promise<ProjectEntity> {
    this.gameRepository.save(project);
    return this.gameRepository.findOne({ where: { id: project.id } });
  }
}
