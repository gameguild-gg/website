import { QuizEntity } from './entities/quiz.entity';

import { QuizService } from './quiz.service';
import { Crud, CrudController } from '@dataui/crud';
import { ApiTags } from '@nestjs/swagger';
import { Controller } from '@nestjs/common';

@Crud({
  model: {
    type: QuizEntity,
  },
  params: {
    id: {
      field: 'id',
      type: 'string',
      primary: true,
    },
  },
})
@Controller('courses/:courseid/quiz')
@ApiTags('quiz')
export class QuizController implements CrudController<QuizEntity> {
  constructor(public readonly service: QuizService) {}
}
