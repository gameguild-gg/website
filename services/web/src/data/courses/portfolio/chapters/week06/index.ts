import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture from './lecture.md';
import assignment from './assignment.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week06lectures: LectureEntity[] = [];
week06lectures.push(createLecture('6-1', 'cover-letter', 'Cover Letter', 'Cover Letter', lecture, 1) as LectureEntity);

week06lectures.push(createLecture('6-2', 'cover-letter-assignment', 'Cover Letter Assignment', 'Cover Letter Assignment', assignment, 2) as LectureEntity);

const Chapter06 = createChapter(
  '6',
  'week06',
  'Week 06: Cover Letter',
  'Writing an effective cover letter is essential to landing a job. In this week, you will learn how to write a cover letter that will help you stand out from the competition.',
  6,
  ['6-1', '6-2'],
  week06lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week06lectures.length; i++) {
  week06lectures[i].chapter = Chapter06;
}

export default Chapter06;
