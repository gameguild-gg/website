import { Api } from '@game-guild/apiclient';
import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import lecture2 from './lecture2.md';
import ChapterEntity = Api.ChapterEntity;
import LectureEntity = Api.LectureEntity;

const week05lectures: LectureEntity[] = [];

week05lectures.push(createLecture('5-1', 'dynamic-goap-cpp', 'Dynamic GOAP in C++', 'Modifying GOAP plans at runtime in C++', lecture, 1) as LectureEntity);

week05lectures.push(createLecture('5-2', 'dynamic-goap-cs', 'Dynamic GOAP in C#', 'Modifying GOAP plans at runtime in C#', lecture2, 2) as LectureEntity);

const Chapter05 = createChapter('5', 'week05', 'Week 5: Dynamic GOAP', 'Dynamic GOAP.', 5, ['5-1', '5-2'], week05lectures) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week05lectures.length; i++) {
  week05lectures[i].chapter = Chapter05;
}

export default Chapter05;
