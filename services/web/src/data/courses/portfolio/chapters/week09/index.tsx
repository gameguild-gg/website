import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture from './lecture.md';
import assignment from './assignment.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week09lectures: LectureEntity[] = [];
week09lectures.push(createLecture('9-1', 'finalizing-demo-reels', 'Finalizing your Demo Reels', 'Finalizing your Demo Reels', lecture, 1) as LectureEntity);

week09lectures.push(
  createLecture(
    '9-2',
    'finalizing-demo-reels-assignment',
    'Finalizing your Demo Reels Assignment',
    'Finalizing your Demo Reels Assignment',
    assignment,
    2,
  ) as LectureEntity,
);

const Chapter09 = createChapter(
  '9',
  'week09',
  'Week 09: Finalizing your Demo Reels',
  'Finalizing your Demo Reels',
  9,
  ['9-1', '9-2'],
  week09lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week09lectures.length; i++) {
  week09lectures[i].chapter = Chapter09;
}

export default Chapter09;
