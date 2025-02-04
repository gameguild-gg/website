import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week04lectures: LectureEntity[] = [];

week04lectures.push(
  createLecture(
    '4-1',
    'goap',
    'GOAP',
    'From A* to GOAP (Goal-Oriented Action Planning) in games.',
    lecture,
    1,
  ) as LectureEntity,
);

const Chapter04 = createChapter(
  '4',
  'week04',
  'Week 4: GOAP',
  'GOAP in games.',
  4,
  ['4-1'],
  week04lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week04lectures.length; i++) {
  week04lectures[i].chapter = Chapter04;
}

export default Chapter04;
