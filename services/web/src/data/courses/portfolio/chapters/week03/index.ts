import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;
import lecture03 from './lecture.md';
import assignment04 from './assignment.md';

const week03lectures: LectureEntity[] = [];
week03lectures.push(
  createLecture(
    '3-1',
    'linkedin-profile',
    'LinkedIn Profile',
    'Setting up your LinkedIn profile.',
    lecture03,
    1,
  ) as LectureEntity,
);

week03lectures.push(
  createLecture(
    '2-2',
    'assignment-04',
    'Assignment 04',
    'LinkedIn Profile Assignment',
    assignment04,
    2,
  ) as LectureEntity,
);

const Chapter03 = createChapter(
  '3',
  'week03',
  'Week 03: LinkedIn Profile',
  'This week we will focus on setting up your LinkedIn profile.',
  3,
  ['3-1', '3-2'],
  week03lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week03lectures.length; i++) {
  week03lectures[i].chapter = Chapter03;
}

export default Chapter03;
