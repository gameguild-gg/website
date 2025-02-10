import { createChapter, createLecture } from '@/data/coursesLib';
import lists from './lists.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week05lectures: LectureEntity[] = [];

week05lectures.push(createLecture('5-1', 'lists', 'Lists', 'Lists in Python programming.', lists, 1) as LectureEntity);

const chapter05: ChapterEntity = createChapter('5', 'week05', 'Week 05: Lists', 'Lists in Python programming.', 5, ['5-1'], week05lectures) as ChapterEntity;

for (let i = 0; i < week05lectures.length; i++) {
  week05lectures[i].chapter = chapter05;
}

export default chapter05;
