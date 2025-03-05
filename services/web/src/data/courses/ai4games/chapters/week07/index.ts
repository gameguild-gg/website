import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week07lectures: LectureEntity[] = [];

week07lectures.push(createLecture('7-1', 'analytics', 'Game Analytics', 'Game Metrics and Analytics', lecture, 1) as LectureEntity);

const Chapter07 = createChapter('7', 'week07', 'Week 7: Game Analytics', 'Game Metrics and Analytics.', 7, ['7-1'], week07lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week07lectures.length; i++) {
  week07lectures[i].chapter = Chapter07;
}

export default Chapter07;
