import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture04 from './lecture.md';
import assignment06 from './assignment06.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week04lectures: LectureEntity[] = [];
week04lectures.push(createLecture('4-1', 'github-readme', 'Effective Github Readme', 'Write an effective Github Readme', lecture04, 1) as LectureEntity);

week04lectures.push(createLecture('4-2', 'assignment-06', 'Assignment 06', 'Github Readme Assignment', assignment06, 2) as LectureEntity);

const Chapter04 = createChapter(
  '4',
  'week04',
  'Week 04: Github Readme',
  'This week we will focus on setting repositories on Github and writing effective Github Readmes',
  4,
  ['4-1', '4-2'],
  week04lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week04lectures.length; i++) {
  week04lectures[i].chapter = Chapter04;
}

export default Chapter04;
