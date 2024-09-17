import { Injectable } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { ProjectEntity } from './entities/project.entity';

@Injectable()
export class GameService {
  constructor(
    @InjectRepository(ProjectEntity)
    private readonly projectRepository: Repository<ProjectEntity>,
  ) {}

  async create(projectData: Partial<ProjectEntity>): Promise<ProjectEntity> {
    const project = this.projectRepository.create(projectData);
    return this.projectRepository.save(project);
  }

  async findAll(): Promise<ProjectEntity[]> {
    return this.projectRepository.find({ relations: ['tickets'] });
  }
}
