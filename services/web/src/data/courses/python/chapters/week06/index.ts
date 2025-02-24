import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week06lectures: LectureEntity[] = [];

week06lectures.push(createLecture('6-1', 'loops', 'loops', 'Loops in Python programming.', lecture, 1) as LectureEntity);

const chapter06: ChapterEntity = createChapter('6', 'week06', 'Week 06: Loops', 'Loops in Python programming.', 6, ['6-1'], week06lectures) as ChapterEntity;

for (let i = 0; i < week06lectures.length; i++) {
  week06lectures[i].chapter = chapter06;
}

export default chapter06;
