import { createChapter, createLecture } from '@/data/coursesLib';
import { Api } from '@game-guild/apiclient';
import lecture07 from './lecture.md';
import assignment07 from './assignment07.md';
import activity from './activity.md';
import LectureEntity = Api.LectureEntity;
import ChapterEntity = Api.ChapterEntity;

const week05lectures: LectureEntity[] = [];
week05lectures.push(createLecture('5-1', 'demo-reels', 'Effective Demo Reels', 'Create an Effective Demo Reels', lecture07, 1) as LectureEntity);

week05lectures.push(createLecture('5-2', 'activity-demo-reels', 'Activity Demo Reels', 'Demo Reels Activity', activity, 2) as LectureEntity);

week05lectures.push(createLecture('5-3', 'assignment-07', 'Demo Reels Assignment', 'Demo Reels Assignment', assignment07, 3) as LectureEntity);

const Chapter05 = createChapter(
  '5',
  'week05',
  'Week 05: Demo Reels',
  'This week we will focus on creating effective demo reels',
  5,
  ['5-1', '5-2', '5-3'],
  week05lectures,
) as ChapterEntity;

// set chapter for each lecture
for (let i = 0; i < week05lectures.length; i++) {
  week05lectures[i].chapter = Chapter05;
}

export default Chapter05;
