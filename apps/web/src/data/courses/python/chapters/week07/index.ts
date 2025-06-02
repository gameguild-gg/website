import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week07lectures: LectureEntity[] = [];

week07lectures.push(createLecture('7-1', 'nested-loops', 'Nested Loops', 'Nested Loops in Python programming.', lecture, 1) as LectureEntity);

const chapter07: ChapterEntity = createChapter(
  '7',
  'week07',
  'Week 07: Nested Loops',
  'Nested Loops in Python programming.',
  7,
  ['7-1'],
  week07lectures,
) as ChapterEntity;

for (let i = 0; i < week07lectures.length; i++) {
  week07lectures[i].chapter = chapter07;
}

export default chapter07;
