import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week06lectures: LectureEntity[] = [];

week06lectures.push(createLecture('6-1', 'htn', 'Hierarchical Task Networks', 'Hierarchical Task Networks in games.', lecture, 1) as LectureEntity);

const Chapter06 = createChapter('6', 'week06', 'Week 6: HTN', 'Hierarchical Task Networks.', 6, ['6-1'], week06lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week06lectures.length; i++) {
  week06lectures[i].chapter = Chapter06;
}

export default Chapter06;
