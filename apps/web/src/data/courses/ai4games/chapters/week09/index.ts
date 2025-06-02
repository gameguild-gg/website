import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week09lectures: LectureEntity[] = [];

week09lectures.push(createLecture('9-1', 'chess', 'Chess', 'Chess', lecture, 1) as LectureEntity);

const Chapter09 = createChapter('9', 'week09', 'Week 9: Chess', 'Chess.', 9, ['9-1'], week09lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week09lectures.length; i++) {
  week09lectures[i].chapter = Chapter09;
}

export default Chapter09;
