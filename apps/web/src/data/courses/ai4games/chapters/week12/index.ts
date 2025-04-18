import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week12lectures: LectureEntity[] = [];

week12lectures.push(createLecture('12-1', 'minmax', 'MinMax', 'Min-Max', lecture, 1) as LectureEntity);

const Chapter12 = createChapter('12', 'week12', 'Week 12: Min Max', 'Min Max Search', 12, ['12-1'], week12lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week12lectures.length; i++) {
  week12lectures[i].chapter = Chapter12;
}

export default Chapter12;
