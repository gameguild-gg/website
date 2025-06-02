import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week13lectures: LectureEntity[] = [];

week13lectures.push(createLecture('13-1', 'mcts', 'MCTS', 'Monte Carlo Tree Search', lecture, 1) as LectureEntity);

const Chapter13 = createChapter('13', 'week13', 'Week 13: MCTS', 'Monte Carlo Tree Search', 13, ['13-1'], week13lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week13lectures.length; i++) {
  week13lectures[i].chapter = Chapter13;
}

export default Chapter13;
