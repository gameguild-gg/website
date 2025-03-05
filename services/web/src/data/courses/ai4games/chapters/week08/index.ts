import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week08lectures: LectureEntity[] = [];

week08lectures.push(createLecture('8-1', 'multi-agent-goap', 'GOAP Multi Agent', 'Extending GOAP into multi agent system', lecture, 1) as LectureEntity);

const Chapter08 = createChapter('8', 'week08', 'Week 8: Extending GOAP', 'Extending GOAP.', 8, ['8-1'], week08lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week08lectures.length; i++) {
  week08lectures[i].chapter = Chapter08;
}

export default Chapter08;
