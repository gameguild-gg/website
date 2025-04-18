import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import assignment from './assignment.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week10lectures: LectureEntity[] = [];
week10lectures.push(createLecture('10-1', 'reviewing-portfolios', 'Finalizing your Portfolio', 'Finalizing your Portfolio', assignment, 1) as LectureEntity);

const Chapter10 = createChapter(
  '10',
  'week10',
  'Week 10: Finalizing your Portfolio',
  'Finalizing your Portfolio',
  10,
  ['10-1'],
  week10lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week10lectures.length; i++) {
  week10lectures[i].chapter = Chapter10;
}

export default Chapter10;
