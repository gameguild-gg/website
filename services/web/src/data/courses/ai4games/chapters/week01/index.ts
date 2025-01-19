import { createChapter, createLecture } from '@/data/coursesLib';
import lecture01 from './lecture.md';
import readings01 from './readings.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week01lectures: LectureEntity[] = [];
week01lectures.push(
  createLecture(
    '1-1',
    'randomness',
    'Random and Noise',
    'Random and Noise!',
    lecture01,
    1,
  ) as LectureEntity,
);

// todo: bug, this not being rendered
week01lectures.push(
  createLecture(
    '1-2',
    'readings01',
    'Readings for Week 1',
    'Readings for Week 1',
    readings01,
    2,
  ) as LectureEntity,
);

const Chapter01 = createChapter(
  '1',
  'week01',
  'Week 1: Noise and Randomness',
  'This week we will review basic concepts of AI for games and introduce the topics.',
  1,
  ['1-1', '1-2'],
  week01lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week01lectures.length; i++) {
  week01lectures[i].chapter = Chapter01;
}

export default Chapter01;
