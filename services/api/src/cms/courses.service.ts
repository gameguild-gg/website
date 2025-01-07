import { Injectable, Logger } from '@nestjs/common';
import { CourseEntity } from './entities/course.entity';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { WithRolesService } from './with-roles.service';

@Injectable()
export class CourseService extends WithRolesService<CourseEntity> {
  private readonly logger = new Logger(CourseService.name);
  constructor(
    @InjectRepository(CourseEntity)
    private readonly repository: Repository<CourseEntity>,
  ) {
    super(repository);
  }
  async save(Course: CourseEntity): Promise<CourseEntity> {
    await this.repository.save(Course);
    return this.repository.findOne({ where: { id: Course.id } });
  }
}
