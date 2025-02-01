import { createChapter, createLecture } from '@/data/coursesLib';
import lecture03 from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week03lectures: LectureEntity[] = [];

week03lectures.push(
  createLecture(
    '3-1',
    'functions-and-math',
    'Functions and Math',
    'Functions and math in Python programming.',
    lecture03,
    1,
  ) as LectureEntity,
);

const chapter03: ChapterEntity = createChapter(
  '3',
  'week03',
  'Week 03: Functions and Math',
  'This week we will cover functions and math in Python programming.',
  3,
  ['3-1'],
  week03lectures,
) as ChapterEntity;

for (let i = 0; i < week03lectures.length; i++) {
  week03lectures[i].chapter = chapter03;
}

export default chapter03;
