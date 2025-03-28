import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import assignment from './assignment.md';
import board from './board.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week11lectures: LectureEntity[] = [];

week11lectures.push(createLecture('11-1', 'chess-board', 'Chess board', 'Chess Board', board, 1) as LectureEntity);
week11lectures.push(createLecture('11-2', 'chess-move-gen', 'Chess Move Generation', 'Chess Move Generation', assignment, 2) as LectureEntity);

const Chapter11 = createChapter(
  '11',
  'week11',
  'Week 11: Chess Move Generation',
  'Chess Move Generation',
  11,
  ['11-1', '11-2'],
  week11lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week11lectures.length; i++) {
  week11lectures[i].chapter = Chapter11;
}

export default Chapter11;
