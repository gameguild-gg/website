import { createChapter, createLecture } from '@/data/coursesLib';
import lists from './lists.md';
import ex01 from './exercise-lists-01.md';
import ex02 from './exercise-lists-02.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week05lectures: LectureEntity[] = [];

week05lectures.push(createLecture('5-1', 'lists', 'Lists', 'Lists in Python programming.', lists, 1) as LectureEntity);
week05lectures.push(createLecture('5-2', 'two-sum', 'Exercise: Two Sum', 'Exercise: Two Sum.', ex01, 2) as LectureEntity);
week05lectures.push(
  createLecture('5-3', 'search-insert-position', 'Exercise: Search Insert Position', 'Exercise: Search Insert Position.', ex02, 3) as LectureEntity,
);

const chapter05: ChapterEntity = createChapter(
  '5',
  'week05',
  'Week 05: Lists',
  'Lists in Python programming.',
  5,
  ['5-1', '5-2', '5-3'],
  week05lectures,
) as ChapterEntity;

for (let i = 0; i < week05lectures.length; i++) {
  week05lectures[i].chapter = chapter05;
}

export default chapter05;
