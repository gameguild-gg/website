import { createChapter, createLecture } from '@/data/coursesLib';
import lecture02 from './lecture02.md';
import assignment03 from './assignment03.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week02lectures: LectureEntity[] = [];
week02lectures.push(
  createLecture(
    '2-1',
    'portfolio-website',
    'Portfolio Website Foundations',
    'Essentials of building a programmer portfolio website.',
    lecture02,
    1,
  ) as LectureEntity,
);

week02lectures.push(
  createLecture(
    '2-2',
    'assignment-03',
    'Assignment 03',
    'Portfolio Website Wireframe',
    assignment03,
    2,
  ) as LectureEntity,
);

week02lectures.push(
  createLecture(
    '2-3',
    'assignment-04',
    'Assignment 04',
    'Portfolio Website Prototype',
    assignment03,
    3,
  ) as LectureEntity,
);

const Chapter02 = createChapter(
  '2',
  'week02',
  'Week 02: Portfolio Website',
  'This week we will focus on the essentials of building a programmer portfolio website.',
  2,
  ['2-1', '2-2', '2-3'],
  week02lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week02lectures.length; i++) {
  week02lectures[i].chapter = Chapter02;
}

export default Chapter02;
