import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import assignment from './assignment.md';
import lecture from './lecture.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week11lectures: LectureEntity[] = [];
week11lectures.push(createLecture('11-1', 'interviews', 'Acing Interviews', 'Acing Interviews', lecture, 1) as LectureEntity);
week11lectures.push(createLecture('11-2', 'mock-interview', 'Mock Interview', 'Mock Interview', assignment, 2) as LectureEntity);

const Chapter11 = createChapter('11', 'week11', 'Week 11: Interviews', 'Interviews', 11, ['11-1', '11-2'], week11lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week11lectures.length; i++) {
  week11lectures[i].chapter = Chapter11;
}

export default Chapter11;
