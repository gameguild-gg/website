import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture from './lecture.md';
import assignment from './assignment.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week07lectures: LectureEntity[] = [];
week07lectures.push(createLecture('7-1', 'center-piece-touchpoint', 'Center Piece Touchpoint', 'Center Piece Touchpoint', lecture, 1) as LectureEntity);

week07lectures.push(
  createLecture(
    '7-2',
    'center-piece-touchpoint-assignment',
    'Center Piece Touchpoint Assignment',
    'Center Piece Touchpoint Assignment',
    assignment,
    2,
  ) as LectureEntity,
);

const Chapter07 = createChapter(
  '7',
  'week07',
  'Week 07: Center Piece Touchpoint',
  'The center piece of your portfolio is the most important part. In this week, you will learn how to create a center piece that will showcase your skills and experience.',
  7,
  ['7-1', '7-2'],
  week07lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week07lectures.length; i++) {
  week07lectures[i].chapter = Chapter07;
}

export default Chapter07;
