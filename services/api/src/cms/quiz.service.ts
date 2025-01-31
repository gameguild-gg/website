import { TypeOrmCrudService } from '@dataui/crud-typeorm';
import { QuizEntity } from './entities/quiz.entity';
import { Repository } from 'typeorm';
import { InjectRepository } from '@nestjs/typeorm';
import { Injectable } from '@nestjs/common';

@Injectable()
export class QuizService extends TypeOrmCrudService<QuizEntity> {
  constructor(
    @InjectRepository(QuizEntity) protected repo: Repository<QuizEntity>,
  ) {
    super(repo);
  }
}
