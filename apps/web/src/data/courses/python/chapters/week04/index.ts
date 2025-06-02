import { createChapter, createLecture } from '@/data/coursesLib';
import lecture04 from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week04lectures: LectureEntity[] = [];

week04lectures.push(
  createLecture(
    '4-1',
    'flow-control',
    'Flow Control',
    'Flow control in Python programming.',
    lecture04,
    1,
  ) as LectureEntity,
);

const chapter04: ChapterEntity = createChapter(
  '4',
  'week04',
  'Week 04: Flow Control',
  'Flow control in Python programming.',
  4,
  ['4-1'],
  week04lectures,
) as ChapterEntity;

for (let i = 0; i < week04lectures.length; i++) {
  week04lectures[i].chapter = chapter04;
}

export default chapter04;
