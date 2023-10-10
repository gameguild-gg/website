import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { Injectable, Logger } from '@nestjs/common';
import { InjectRepository } from "@nestjs/typeorm";
import { Repository } from "typeorm";
import { CourseEntity } from "./entities/course.entity";

@Injectable()
export class CourseService extends TypeOrmCrudService<CourseEntity> {
  private readonly logger = new Logger(CourseService.name);

  constructor(@InjectRepository(CourseEntity) private readonly repository: Repository<CourseEntity>) {
    super(repository);
  }
}
