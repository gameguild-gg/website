import { createChapter, createLecture } from '@/data/coursesLib';
import lecture01 from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week01lectures: LectureEntity[] = [];

week01lectures.push(
  createLecture(
    '1-1',
    'intro',
    'Course Introduction',
    'Welcome to the Python Programming course!',
    lecture01,
    1,
  ) as LectureEntity,
);

const chapter01: ChapterEntity = createChapter(
  '1',
  'week01',
  'Week 01: Introduction to Python',
  'This week we will introduce the course, ',
  1,
  ['1-1'],
  week01lectures,
) as ChapterEntity;

for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = chapter01;
}

export default chapter01;
