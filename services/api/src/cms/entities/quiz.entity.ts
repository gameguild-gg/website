import { ContentBase } from './content.base';
import { FormShortAnswerDto } from '../dtos/form-short-answer.dto';
import { Column, Entity, OneToMany } from 'typeorm';
import { FormLongAnswerDto } from '../dtos/form-long-answer.dto';
import { ApiExtraModels, ApiProperty, getSchemaPath } from '@nestjs/swagger';

@ApiExtraModels(FormShortAnswerDto, FormLongAnswerDto)
@Entity({ name: 'quiz' })
export class QuizEntity extends ContentBase {
  @Column({ nullable: true, type: 'jsonb' })
  // array of the questions whose could be of different types such as short answer, multiple choice, etc.
  // todo: @matheus add validators to this pls. vulgo: tivira nos 30 ;-)
  @ApiProperty({
    anyOf: [
      { $ref: getSchemaPath(FormShortAnswerDto) },
      { $ref: getSchemaPath(FormLongAnswerDto) },
    ],
    isArray: true,
  })
  questions: (FormShortAnswerDto | FormLongAnswerDto)[];
}
