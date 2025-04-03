import { createChapter, createLecture } from '@/data/coursesLib';
import lecture from './lecture.md';
import localllm from './local-llm.md';
import { Api } from '@game-guild/apiclient';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week12lectures: LectureEntity[] = [];

week12lectures.push(createLecture('12-1', 'api', 'APIs usage', 'Using python to call APIs', lecture, 1) as LectureEntity);

week12lectures.push(createLecture('12-2', 'llms', 'calling llms from python', 'Using python to call LLMs APIs', localllm, 1) as LectureEntity);

const chapter12: ChapterEntity = createChapter(
  '12',
  'week12',
  'Week 12: APIs',
  'Calling APIs from python.',
  12,
  ['12-1', '12-2'],
  week12lectures,
) as ChapterEntity;

for (let i = 0; i < week12lectures.length; i++) {
  week12lectures[i].chapter = chapter12;
}

export default chapter12;
