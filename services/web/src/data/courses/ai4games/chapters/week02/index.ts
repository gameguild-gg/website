import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;
import pcg from './pcg.md';

const week02lectures: LectureEntity[] = [];
week02lectures.push(
  createLecture(
    '2-1',
    'pcg',
    'Procedural Content Generation',
    'Procedural Content Generation',
    pcg,
    1,
    Api.LectureEntity.Type.Enum.Reveal,
  ) as LectureEntity,
);

const Chapter02 = createChapter(
  '2',
  'week02',
  'Week 2: Procedural Content Generation',
  'ProcGen is a powerful technique for creating content in games.',
  2,
  ['2-1'],
  week02lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week02lectures.length; i++) {
  week02lectures[i].chapter = Chapter02;
}

export default Chapter02;
