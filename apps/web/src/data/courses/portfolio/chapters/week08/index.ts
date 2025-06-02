import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture from './lecture.md';
import assignment from './assignment.md';
import activity from './activity.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week08lectures: LectureEntity[] = [];
week08lectures.push(
  createLecture('8-1', 'presenting-visually', 'Presenting Visually your Ideas', 'Presenting Visually your Ideas', lecture, 1) as LectureEntity,
);

week08lectures.push(createLecture('8-2', 'uml-review', 'Using UML for presenting ideas', 'Using UML for presenting ideas', activity, 2) as LectureEntity);

week08lectures.push(
  createLecture(
    '8-3',
    'presenting-visually-assignment',
    'Presenting Visually your Ideas Assignment',
    'Presenting Visually your Ideas Assignment',
    assignment,
    3,
  ) as LectureEntity,
);

const Chapter08 = createChapter(
  '8',
  'week08',
  'Week 08: Presenting your ideas visually',
  'ow to present your ideas visually using UML diagrams and other visual tools.',
  8,
  ['8-1', '8-2', '8-3'],
  week08lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week08lectures.length; i++) {
  week08lectures[i].chapter = Chapter08;
}

export default Chapter08;
