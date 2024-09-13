import { Injectable, Logger } from '@nestjs/common';
import { ProjectEntity } from './entities/project.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { WithRolesService } from './with-roles.service';

@Injectable()
export class GameService extends WithRolesService<ProjectEntity> {
  private readonly logger = new Logger(GameService.name);
  constructor(
    @InjectRepository(ProjectEntity)
    private readonly gameRepository: Repository<ProjectEntity>,
  ) {
    super(gameRepository);
  }
}
