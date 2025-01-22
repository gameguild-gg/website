import { createChapter, createLecture } from '@/data/coursesLib';
import lecture01 from './lecture01.md';
import assignment01 from './assignment01.md';
import assignment02 from './assignment02.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week01lectures: LectureEntity[] = [];
week01lectures.push(
  createLecture(
    '1-1',
    'portfolio',
    'Introduction to Portfolio',
    'Welcome to the Game Developer Portfolio course!',
    lecture01,
    1,
  ) as LectureEntity,
);
week01lectures.push(
  createLecture(
    '1-2',
    'assignment-01',
    'Assignment 01',
    'Solo Project Proposal',
    assignment01,
    2,
  ) as LectureEntity,
);
week01lectures.push(
  createLecture(
    '1-3',
    'assignment-02',
    'Assignment 02',
    'Portfolio Analysis',
    assignment02,
    3,
  ) as LectureEntity,
);

const Chapter01 = createChapter(
  '1',
  'week01',
  'Week 01: Introduction to Portfolio',
  'This week we will introduce the course, plan deliverables and analyze portfolios.',
  1,
  ['1-1', '1-2', '1-3'],
  week01lectures,
) as ChapterEntity;

// set the chapter for each lecture
for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = Chapter01;
}

export default Chapter01;
