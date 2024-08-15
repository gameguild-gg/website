import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { WithRolesService } from '../../with-roles.service';
import { CourseEntity } from '../../entities/Course.entity';

@Injectable()
export class CourseService extends WithRolesService<CourseEntity> {
  private readonly logger = new Logger(CourseService.name);
  constructor(
    @InjectRepository(CourseEntity)
    private readonly courseRepository: Repository<CourseEntity>,
  ) {
    super(courseRepository);
  }
}
