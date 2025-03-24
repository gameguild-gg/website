import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week11lectures: LectureEntity[] = [];

week11lectures.push(createLecture('11-1', 'files', 'Files', 'Files in Python programming.', lecture, 1) as LectureEntity);

const chapter11: ChapterEntity = createChapter('11', 'week11', 'Week 11: Files', 'Files in Python programming.', 11, ['11-1'], week11lectures) as ChapterEntity;

for (let i = 0; i < week11lectures.length; i++) {
  week11lectures[i].chapter = chapter11;
}

export default chapter11;
