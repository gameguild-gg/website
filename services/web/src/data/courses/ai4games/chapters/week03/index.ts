import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import astar from './a-star.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week03lectures: LectureEntity[] = [];

week03lectures.push(
  createLecture(
    '3-1',
    'ai-engines',
    'AI Engines',
    'AI Engines in games.',
    lecture,
    1,
  ) as LectureEntity,
);

week03lectures.push(
  createLecture(
    '3-2',
    'a-star',
    'A* Pathfinding',
    'A* Pathfinding algorithm',
    astar,
    2,
  ) as LectureEntity,
);

const Chapter03 = createChapter(
  '3',
  'week03',
  'Week 3: AI Engines',
  'AI Engines in games.',
  3,
  ['3-1', '3-2'],
  week03lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week03lectures.length; i++) {
  week03lectures[i].chapter = Chapter03;
}

export default Chapter03;
