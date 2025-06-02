import { createChapter, createLecture } from '@/data/coursesLib';
import lecture02 from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week02lectures: LectureEntity[] = [];
week02lectures.push(
  createLecture(
    '2-1',
    'python-basics',
    'Python Basics',
    'An introduction to Python programming basics.',
    lecture02,
    1,
  ) as LectureEntity,
);

const chapter02: ChapterEntity = createChapter(
  '2',
  'week02',
  'Week 02: Python Basics',
  'This week we will cover the basics of Python programming.',
  2,
  ['2-1'],
  week02lectures,
) as ChapterEntity;

for (let i = 0; i < week02lectures.length; i++) {
  week02lectures[i].chapter = chapter02;
}

export default chapter02;
