import { createChapter, createLecture } from '@/data/coursesLib';
import dictionaries from './dictionaries.md';
import sets from './sets.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week10lectures: LectureEntity[] = [];

week10lectures.push(createLecture('10-1', 'dictionaries', 'Dictionaries', 'Dictionaries in Python programming.', dictionaries, 1) as LectureEntity);
week10lectures.push(createLecture('10-2', 'sets', 'Sets', 'Sets in Python programing', sets, 2) as LectureEntity);

const chapter10: ChapterEntity = createChapter(
  '10',
  'week10',
  'Week 10: Dictionaries',
  'Dictionaries in Python programming.',
  10,
  ['10-1', '10-2'],
  week10lectures,
) as ChapterEntity;

for (let i = 0; i < week10lectures.length; i++) {
  week10lectures[i].chapter = chapter10;
}

export default chapter10;
