import { Injectable, Logger } from '@nestjs/common';
import { ProjectEntity } from './entities/project.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { WithRolesService } from './with-roles.service';

@Injectable()
export class ProjectService extends WithRolesService<ProjectEntity> {
  private readonly logger = new Logger(ProjectService.name);
  constructor(
    @InjectRepository(ProjectEntity)
    private readonly gameRepository: Repository<ProjectEntity>,
  ) {
    super(gameRepository);
  }
  async getGameOwner(userName: string): Promise<ProjectEntity[]> {
    return this.gameRepository.find({
      where: { owner: { username: userName } },
    });
  }

  async getAll(): Promise<ProjectEntity[]> {
    return this.gameRepository.find();
  }
}
